using Infrastructure.Adapters.Cache;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using StackExchange.Redis;
using Task = System.Threading.Tasks.Task;

namespace Tests.Infrastructure.Adapters.Cache;

public class RedisCacheImplTests
{
    private record SamplePayload(int InstanceId, string Name);

    private static (RedisCacheImpl cache, Mock<IDatabase> db) Build()
    {
        var dbMock = new Mock<IDatabase>();
        var store = new Dictionary<string, RedisValue>();

        // db.StringSetAsync(key, json, historyTtl) con un TimeSpan literal como 3er argumento
        // resuelve, por conversión implícita, al overload (RedisKey, RedisValue, Expiration,
        // ValueCondition, CommandFlags) — no al que usa TimeSpan?/When — así que hay que
        // mockear esos tipos exactos para que el Setup realmente intercepte la llamada.
        dbMock.Setup(d => d.StringSetAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<Expiration>(), It.IsAny<ValueCondition>(), It.IsAny<CommandFlags>()))
              .Callback<RedisKey, RedisValue, Expiration, ValueCondition, CommandFlags>((k, v, _, _, _) => store[k.ToString()] = v)
              .ReturnsAsync(true);
        dbMock.Setup(d => d.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
              .ReturnsAsync((RedisKey k, CommandFlags _) => store.TryGetValue(k.ToString(), out var v) ? v : RedisValue.Null);
        dbMock.Setup(d => d.KeyDeleteAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
              .ReturnsAsync(true);

        var redisMock = new Mock<IConnectionMultiplexer>();
        redisMock.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(dbMock.Object);

        var cache = new RedisCacheImpl(redisMock.Object, NullLogger<RedisCacheImpl>.Instance);
        return (cache, dbMock);
    }

    // Regresión: Save<T> serializaba value?.ToString() en vez de value, así que cualquier
    // tipo no-string se guardaba como el nombre de la clase en vez de su JSON real.
    [Fact]
    public async Task SaveThenGet_ComplexObject_RoundTripsCorrectly()
    {
        var (cache, _) = Build();

        var payload = new SamplePayload(42, "demo");
        await cache.Save("key1", payload, TimeSpan.FromMinutes(5));
        var result = await cache.Get<SamplePayload>("key1");

        Assert.NotNull(result);
        Assert.Equal(42, result!.InstanceId);
        Assert.Equal("demo", result.Name);
    }

    [Fact]
    public async Task SaveThenGet_IntValue_RoundTripsCorrectly()
    {
        var (cache, _) = Build();

        await cache.Save("instance-state", 7, TimeSpan.FromMinutes(15));
        var result = await cache.Get<int>("instance-state");

        Assert.Equal(7, result);
    }

    [Fact]
    public async Task Get_KeyDoesNotExist_ReturnsDefault()
    {
        var (cache, _) = Build();

        var result = await cache.Get<int>("missing");

        Assert.Equal(0, result);
    }

    [Fact]
    public async Task Delete_CallsKeyDeleteWithGivenKey()
    {
        var (cache, db) = Build();

        await cache.Delete("some-key");

        db.Verify(d => d.KeyDeleteAsync(
            It.Is<RedisKey>(k => k.ToString() == "some-key"),
            It.IsAny<CommandFlags>()), Times.Once);
    }
}
