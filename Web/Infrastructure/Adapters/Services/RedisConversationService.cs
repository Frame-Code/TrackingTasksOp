using Application.Dto.Conversation;
using Application.Ports.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using System.Text.Json;
using Web.Infrastructure.Config.Settings;

namespace Web.Infrastructure.Adapters.Services;

/// <summary>
/// Persiste el contexto de conversación en Redis.
/// Cada sesión se almacena como JSON con TTL configurable.
/// </summary>
public sealed class RedisConversationService : IConversationContextService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly RedisSettings _settings;
    private readonly ILogger<RedisConversationService> _logger;
    private readonly TimeSpan _ttl;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public RedisConversationService(
        IConnectionMultiplexer redis,
        IOptions<RedisSettings> options,
        ILogger<RedisConversationService> logger)
    {
        _redis = redis;
        _settings = options.Value;
        _logger = logger;
        _ttl = TimeSpan.FromMinutes(_settings.ConversationTtlMinutes);
    }

    public async Task<ConversationContext> GetOrCreateAsync(string sessionId, CancellationToken ct = default)
    {
        var key = BuildKey(sessionId);
        var db = _redis.GetDatabase();

        try
        {
            var value = await db.StringGetAsync(key);

            if (value.HasValue)
            {
                var context = JsonSerializer.Deserialize<ConversationContext>(value!, JsonOpts);
                if (context is not null)
                {
                    _logger.LogDebug("Loaded conversation context for session {SessionId}", sessionId);
                    // Refrescar el TTL cada vez que se accede al contexto
                    await db.KeyExpireAsync(key, _ttl);
                    return context;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load context for {SessionId}, creating new one", sessionId);
        }

        _logger.LogInformation("Creating new conversation context for session {SessionId}", sessionId);
        return new ConversationContext { SessionId = sessionId };
    }

    public async Task SaveAsync(ConversationContext context, CancellationToken ct = default)
    {
        var key = BuildKey(context.SessionId);
        var db = _redis.GetDatabase();

        try
        {
            var json = JsonSerializer.Serialize(context, JsonOpts);
            await db.StringSetAsync(key, json, _ttl);
            _logger.LogDebug("Saved context for session {SessionId}, TTL={TTL}min",
                context.SessionId, _settings.ConversationTtlMinutes);
        }
        catch (Exception ex)
        {
            // No fallar la petición si Redis falla — degradar gracefully
            _logger.LogError(ex, "Failed to save context for {SessionId}", context.SessionId);
        }
    }

    public async Task DeleteAsync(string sessionId, CancellationToken ct = default)
    {
        var key = BuildKey(sessionId);
        var db = _redis.GetDatabase();
        await db.KeyDeleteAsync(key);
        _logger.LogInformation("Deleted conversation context for session {SessionId}", sessionId);
    }

    private string BuildKey(string sessionId) => $"{_settings.KeyPrefix}:{sessionId}";
}
