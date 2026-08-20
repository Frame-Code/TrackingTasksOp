using Infrastructure.Adapters.Services.Bot;
using Infrastructure.DataAccess.Entities;
using Infrastructure.Settings;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using StackExchange.Redis;
using Task = System.Threading.Tasks.Task;

namespace Tests.Infrastructure.Adapters.Services.Bot;

public class AiUsageLimiterImplTests
{
    private static Mock<UserManager<ApplicationUser>> BuildUserManagerMock(ApplicationUser? user)
    {
        var store = new Mock<IUserStore<ApplicationUser>>();
#pragma warning disable CS8625
        var mock = new Mock<UserManager<ApplicationUser>>(
            store.Object, null, null, null, null, null, null, null, null);
#pragma warning restore CS8625
        mock.Setup(x => x.FindByIdAsync("user-1")).ReturnsAsync(user);
        return mock;
    }

    private static (AiUsageLimiterImpl limiter, Mock<IDatabase> db) Build(
        ApplicationUser? user, int dailyLimit = 3)
    {
        var dbMock = new Mock<IDatabase>();
        var redisMock = new Mock<IConnectionMultiplexer>();
        redisMock.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(dbMock.Object);

        var settings = Options.Create(new GroqSettings
        {
            ApiKey = "shared-key",
            Model = "m",
            BaseUrl = "http://groq.example.com",
            DailyMessageLimitPerUser = dailyLimit
        });

        var limiter = new AiUsageLimiterImpl(
            redisMock.Object, settings, BuildUserManagerMock(user).Object, NullLogger<AiUsageLimiterImpl>.Instance);

        return (limiter, dbMock);
    }

    private static ApplicationUser BuildUser(string? encryptedGroqApiKey = null) =>
        new() { Id = "user-1", Email = "user@test.com", EncryptedGroqApiKey = encryptedGroqApiKey };

    [Fact]
    public async Task TryConsumeAsync_UsuarioConKeyPropia_DevuelveTrueSinTocarRedis()
    {
        var (limiter, db) = Build(BuildUser(encryptedGroqApiKey: "cipher-text"));

        var result = await limiter.TryConsumeAsync("user-1");

        Assert.True(result);
        db.Verify(d => d.StringIncrementAsync(It.IsAny<RedisKey>(), It.IsAny<long>(), It.IsAny<CommandFlags>()), Times.Never);
    }

    [Fact]
    public async Task TryConsumeAsync_LimiteDesactivado_DevuelveTrueSinTocarRedis()
    {
        var (limiter, db) = Build(BuildUser(), dailyLimit: 0);

        var result = await limiter.TryConsumeAsync("user-1");

        Assert.True(result);
        db.Verify(d => d.StringIncrementAsync(It.IsAny<RedisKey>(), It.IsAny<long>(), It.IsAny<CommandFlags>()), Times.Never);
    }

    [Fact]
    public async Task TryConsumeAsync_DebajoDelLimite_DevuelveTrue()
    {
        var (limiter, db) = Build(BuildUser(), dailyLimit: 3);
        db.Setup(d => d.StringIncrementAsync(It.IsAny<RedisKey>(), 1, It.IsAny<CommandFlags>())).ReturnsAsync(2);

        var result = await limiter.TryConsumeAsync("user-1");

        Assert.True(result);
    }

    [Fact]
    public async Task TryConsumeAsync_SuperaElLimite_DevuelveFalse()
    {
        var (limiter, db) = Build(BuildUser(), dailyLimit: 3);
        db.Setup(d => d.StringIncrementAsync(It.IsAny<RedisKey>(), 1, It.IsAny<CommandFlags>())).ReturnsAsync(4);

        var result = await limiter.TryConsumeAsync("user-1");

        Assert.False(result);
    }

    [Fact]
    public async Task TryConsumeAsync_PrimerUsoDelDia_LeAplicaExpiracion()
    {
        var (limiter, db) = Build(BuildUser(), dailyLimit: 3);
        db.Setup(d => d.StringIncrementAsync(It.IsAny<RedisKey>(), 1, It.IsAny<CommandFlags>())).ReturnsAsync(1);

        await limiter.TryConsumeAsync("user-1");

        db.Verify(d => d.KeyExpireAsync(It.IsAny<RedisKey>(), It.IsAny<TimeSpan?>(), It.IsAny<ExpireWhen>(), It.IsAny<CommandFlags>()), Times.Once);
    }

    [Fact]
    public async Task TryConsumeAsync_RedisFalla_DevuelveTrue_NoBloqueaPorInfraestructura()
    {
        var (limiter, db) = Build(BuildUser(), dailyLimit: 3);
        db.Setup(d => d.StringIncrementAsync(It.IsAny<RedisKey>(), 1, It.IsAny<CommandFlags>()))
          .ThrowsAsync(new RedisConnectionException(ConnectionFailureType.UnableToConnect, "down"));

        var result = await limiter.TryConsumeAsync("user-1");

        Assert.True(result);
    }
}
