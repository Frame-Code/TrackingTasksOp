using System.Text.Json;
using Application.Dto.Conversation;
using Application.Ports.Auth;
using Infrastructure.Adapters.Services;
using Infrastructure.Settings;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using StackExchange.Redis;
using Task = System.Threading.Tasks.Task;

namespace Tests.Infrastructure.Adapters.Services;

public class RedisConversationServiceTests
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    private sealed class FakeCurrentUser(string? userId) : CurrentUser
    {
        public override string? UserId { get; } = userId;
        public override bool IsAuthenticated => UserId is not null;
        public override string? OpenProjectInstanceUrl => null;
        public override int? OpenProjectInstanceId => null;
        public override int? OpenProjectUserId => null;
    }

    private static (RedisConversationService svc, Mock<IDatabase> db) Build(string userId = "user1")
    {
        var settings = Options.Create(new RedisSettings
        {
            Configuration  = "localhost",
            InstanceName   = "test",
            KeyPrefix      = "ttop",
            HistoryTtlDays = 30
        });

        var dbMock = new Mock<IDatabase>();
        dbMock.Setup(d => d.KeyExpireAsync(It.IsAny<RedisKey>(), It.IsAny<TimeSpan?>(), It.IsAny<CommandFlags>()))
              .ReturnsAsync(true);
        dbMock.Setup(d => d.StringSetAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<TimeSpan?>(), It.IsAny<bool>(), It.IsAny<When>(), It.IsAny<CommandFlags>()))
              .ReturnsAsync(true);
        // The 3-arg call dispatches through IDatabaseAsync (5-param overload), not IDatabase (6-param)
        dbMock.As<IDatabaseAsync>()
              .Setup(d => d.StringSetAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<TimeSpan?>(), It.IsAny<When>(), It.IsAny<CommandFlags>()))
              .ReturnsAsync(true);
        dbMock.Setup(d => d.SortedSetAddAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<double>(), It.IsAny<SortedSetWhen>(), It.IsAny<CommandFlags>()))
              .ReturnsAsync(true);
        dbMock.Setup(d => d.KeyDeleteAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
              .ReturnsAsync(true);
        dbMock.Setup(d => d.SortedSetRemoveAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<CommandFlags>()))
              .ReturnsAsync(true);
        dbMock.Setup(d => d.SortedSetRangeByRankAsync(It.IsAny<RedisKey>(), It.IsAny<long>(), It.IsAny<long>(), It.IsAny<Order>(), It.IsAny<CommandFlags>()))
              .ReturnsAsync(Array.Empty<RedisValue>());
        dbMock.Setup(d => d.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
              .ReturnsAsync(RedisValue.Null);

        var redisMock = new Mock<IConnectionMultiplexer>();
        redisMock.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(dbMock.Object);

        var svc = new RedisConversationService(
            redisMock.Object,
            settings,
            NullLogger<RedisConversationService>.Instance,
            new FakeCurrentUser(userId));

        return (svc, dbMock);
    }

    // ── GetOrCreateAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task GetOrCreateAsync_DevuelveNuevoContexto_CuandoNoExisteEnRedis()
    {
        var (svc, _) = Build();

        var ctx = await svc.GetOrCreateAsync("sess1");

        Assert.Equal("sess1", ctx.SessionId);
        Assert.Empty(ctx.History);
        Assert.Equal("Nuevo chat", ctx.Title);
    }

    [Fact]
    public async Task GetOrCreateAsync_DevuelveContextoExistente_CuandoExisteEnRedis()
    {
        var (svc, db) = Build();
        // AddUserMessage sets Title from the first message
        var stored = new ConversationContext { SessionId = "sess2" };
        stored.AddUserMessage("Hola mundo");
        db.Setup(d => d.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
          .ReturnsAsync(new RedisValue(JsonSerializer.Serialize(stored, JsonOpts)));

        var ctx = await svc.GetOrCreateAsync("sess2");

        Assert.Equal("sess2", ctx.SessionId);
        Assert.Equal("Hola mundo", ctx.Title);
        Assert.Single(ctx.History);
    }

    // ── SaveAsync ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetOrCreateAsync_UsaClaveAcotadaAlUsuario_EnStringGet()
    {
        // StringGetAsync (lectura) usa la misma BuildKey que StringSetAsync (escritura).
        // Verificar aquí es equivalente a verificar la escritura, y evita la ambigüedad
        // de overloads de StringSetAsync entre IDatabase e IDatabaseAsync en Moq.
        var (svc, db) = Build("alice");

        await svc.GetOrCreateAsync("my-session");

        db.Verify(d => d.StringGetAsync(
            It.Is<RedisKey>(k => k.ToString().Contains("alice") && k.ToString().Contains("my-session")),
            It.IsAny<CommandFlags>()), Times.Once);
    }

    [Fact]
    public async Task SaveAsync_AgregaAlIndiceOrdenadoPorUsuario()
    {
        var (svc, db) = Build("alice");

        await svc.SaveAsync(new ConversationContext { SessionId = "sessX" });

        db.Verify(d => d.SortedSetAddAsync(
            It.Is<RedisKey>(k => k.ToString().Contains("alice") && k.ToString().Contains("index")),
            It.Is<RedisValue>(v => v.ToString() == "sessX"),
            It.IsAny<double>(),
            It.IsAny<SortedSetWhen>(),
            It.IsAny<CommandFlags>()), Times.Once);
    }

    [Fact]
    public async Task SaveAsync_AislaClavesPorUsuario_IndiceContieneSoloSuUserId()
    {
        // Capturamos la clave del índice (SortedSetAddAsync) que SÍ funciona con Moq,
        // en lugar de StringSetAsync cuya resolución de overload genera ambigüedad.
        var (svcA, dbA) = Build("alice");
        var (svcB, dbB) = Build("bob");

        string? idxA = null, idxB = null;

        dbA.Setup(d => d.SortedSetAddAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<double>(), It.IsAny<SortedSetWhen>(), It.IsAny<CommandFlags>()))
           .Callback((RedisKey k, RedisValue _, double __, SortedSetWhen ___, CommandFlags ____) => idxA = k.ToString())
           .ReturnsAsync(true);

        dbB.Setup(d => d.SortedSetAddAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<double>(), It.IsAny<SortedSetWhen>(), It.IsAny<CommandFlags>()))
           .Callback((RedisKey k, RedisValue _, double __, SortedSetWhen ___, CommandFlags ____) => idxB = k.ToString())
           .ReturnsAsync(true);

        var ctx = new ConversationContext { SessionId = "same-sess" };
        await svcA.SaveAsync(ctx);
        await svcB.SaveAsync(ctx);

        Assert.NotNull(idxA);
        Assert.NotNull(idxB);
        Assert.NotEqual(idxA, idxB);
        Assert.Contains("alice", idxA);
        Assert.Contains("bob", idxB);
    }

    // ── ListAsync ────────────────────────────────────────────────────────────

    [Fact]
    public async Task ListAsync_DevuelveListaVacia_SiElIndiceEstaVacio()
    {
        var (svc, _) = Build();

        var result = await svc.ListAsync();

        Assert.Empty(result);
    }

    [Fact]
    public async Task ListAsync_RetornaResumenesDesdeLasClavesDeStrings()
    {
        var (svc, db) = Build("user1");
        var ctx1 = new ConversationContext { SessionId = "s1", Title = "Chat 1" };
        var ctx2 = new ConversationContext { SessionId = "s2", Title = "Chat 2" };

        db.Setup(d => d.SortedSetRangeByRankAsync(It.IsAny<RedisKey>(), 0, -1, Order.Descending, It.IsAny<CommandFlags>()))
          .ReturnsAsync([new RedisValue("s1"), new RedisValue("s2")]);

        db.Setup(d => d.StringGetAsync(It.Is<RedisKey>(k => k.ToString().EndsWith(":s1")), It.IsAny<CommandFlags>()))
          .ReturnsAsync(new RedisValue(JsonSerializer.Serialize(ctx1, JsonOpts)));

        db.Setup(d => d.StringGetAsync(It.Is<RedisKey>(k => k.ToString().EndsWith(":s2")), It.IsAny<CommandFlags>()))
          .ReturnsAsync(new RedisValue(JsonSerializer.Serialize(ctx2, JsonOpts)));

        var result = await svc.ListAsync();

        Assert.Equal(2, result.Count);
        Assert.Equal("s1", result[0].SessionId);
        Assert.Equal("Chat 1", result[0].Title);
    }

    [Fact]
    public async Task ListAsync_LimpiaSesionExpirada_DelIndice()
    {
        var (svc, db) = Build();
        db.Setup(d => d.SortedSetRangeByRankAsync(It.IsAny<RedisKey>(), 0, -1, Order.Descending, It.IsAny<CommandFlags>()))
          .ReturnsAsync([new RedisValue("expired-sess")]);
        // StringGetAsync devuelve Null (default en Build → entrada expirada)

        var result = await svc.ListAsync();

        Assert.Empty(result);
        db.Verify(d => d.SortedSetRemoveAsync(
            It.IsAny<RedisKey>(),
            new RedisValue("expired-sess"),
            It.IsAny<CommandFlags>()), Times.Once);
    }

    // ── DeleteAsync ──────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteAsync_EliminaStringYEntradaDeIndice()
    {
        var (svc, db) = Build("userX");

        await svc.DeleteAsync("sess-del");

        db.Verify(d => d.KeyDeleteAsync(
            It.Is<RedisKey>(k => k.ToString().Contains("userX") && k.ToString().Contains("sess-del")),
            It.IsAny<CommandFlags>()), Times.Once);

        db.Verify(d => d.SortedSetRemoveAsync(
            It.Is<RedisKey>(k => k.ToString().Contains("index") && k.ToString().Contains("userX")),
            new RedisValue("sess-del"),
            It.IsAny<CommandFlags>()), Times.Once);
    }
}
