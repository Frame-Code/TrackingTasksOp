using System.Text.Json;
using Application.Ports.Cache;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Infrastructure.Adapters.Cache;

public class RedisCacheImpl(
    IConnectionMultiplexer redis,
    ILogger<RedisCacheImpl> logger) : IRedisCache
{
    private static readonly JsonSerializerOptions? JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };
    
    public async Task Save<T>(string key, T value, TimeSpan historyTtl)
    {
        var db = redis.GetDatabase();
        try
        {
            var json = JsonSerializer.Serialize(value, JsonOpts);
            await db.StringSetAsync(key, json, historyTtl);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to save cache key: {Key} and value: {Value}", key, value?.ToString() ?? "-");
        }
    }

    public async Task<T?> Get<T>(string key)
    {
        var db = redis.GetDatabase();
        try
        {
            var value = await db.StringGetAsync(key);
            if (value.HasValue) return JsonSerializer.Deserialize<T>(value!, JsonOpts);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to get Cache Key: {key} ", key);
        }

        return await Task.FromResult<T>(default!);
    }

    public async Task Delete(string key)
    {
        var db = redis.GetDatabase();
        await db.KeyDeleteAsync(key);
        logger.LogInformation("Deleted cache key {key}", key);
    }
}