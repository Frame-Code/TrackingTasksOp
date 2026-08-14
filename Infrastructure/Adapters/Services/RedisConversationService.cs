using System.Text.Json;
using Application.Dto.Conversation;
using Application.Ports.Auth;
using Application.Ports.Services;
using Infrastructure.Settings;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Infrastructure.Adapters.Services;

/// <summary>
/// Persiste el contexto de conversación en Redis. Las claves están acotadas al usuario
/// (el sessionId lo genera el cliente y por sí solo no autoriza a leer nada), y un índice
/// ordenado por usuario permite listar sus chats sin escanear claves.
/// </summary>
public sealed class RedisConversationService : IConversationContextService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly RedisSettings _settings;
    private readonly ILogger<RedisConversationService> _logger;
    private readonly CurrentUser _currentUser;
    private readonly TimeSpan _historyTtl;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public RedisConversationService(
        IConnectionMultiplexer redis,
        IOptions<RedisSettings> options,
        ILogger<RedisConversationService> logger,
        CurrentUser currentUser)
    {
        _redis = redis;
        _settings = options.Value;
        _logger = logger;
        _currentUser = currentUser;
        _historyTtl = TimeSpan.FromDays(Math.Max(1, _settings.HistoryTtlDays));
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
                    await db.KeyExpireAsync(key, _historyTtl);
                    return context;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load context for {SessionId}, creating new one", sessionId);
        }

        return new ConversationContext { SessionId = sessionId };
    }

    public async Task SaveAsync(ConversationContext context, CancellationToken ct = default)
    {
        var db = _redis.GetDatabase();
        try
        {
            var json = JsonSerializer.Serialize(context, JsonOpts);
            await db.StringSetAsync(BuildKey(context.SessionId), json, _historyTtl);

            // Índice ordenado por fecha para listar los chats sin escanear claves.
            var indexKey = BuildIndexKey();
            await db.SortedSetAddAsync(indexKey, context.SessionId, context.LastUpdatedAt.Ticks);
            await db.KeyExpireAsync(indexKey, _historyTtl);
        }
        catch (Exception ex)
        {
            // No fallar la petición si Redis falla — degradar gracefully
            _logger.LogError(ex, "Failed to save context for {SessionId}", context.SessionId);
        }
    }

    public async Task DeleteAsync(string sessionId, CancellationToken ct = default)
    {
        var db = _redis.GetDatabase();
        await db.KeyDeleteAsync(BuildKey(sessionId));
        await db.SortedSetRemoveAsync(BuildIndexKey(), sessionId);
        _logger.LogInformation("Deleted conversation {SessionId}", sessionId);
    }

    public async Task<List<ConversationSummary>> ListAsync(CancellationToken ct = default)
    {
        var db = _redis.GetDatabase();
        var summaries = new List<ConversationSummary>();

        try
        {
            var ids = await db.SortedSetRangeByRankAsync(BuildIndexKey(), 0, -1, Order.Descending);
            foreach (var id in ids)
            {
                var value = await db.StringGetAsync(BuildKey(id!));
                if (!value.HasValue)
                {
                    // Expiró el chat pero quedó en el índice: lo limpiamos.
                    await db.SortedSetRemoveAsync(BuildIndexKey(), id);
                    continue;
                }

                var context = JsonSerializer.Deserialize<ConversationContext>(value!, JsonOpts);
                if (context is not null)
                    summaries.Add(new ConversationSummary(context.SessionId, context.Title, context.LastUpdatedAt));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list conversations for user {UserId}", _currentUser.UserId);
        }

        return summaries;
    }

    // El userId acota las claves al usuario; si no está autenticado degradamos a "anon"
    // (los endpoints del bot ya exigen auth, así que en la práctica siempre hay userId).
    private string UserScope => _currentUser.UserId ?? "anon";
    private string BuildKey(string sessionId) => $"{_settings.KeyPrefix}:{UserScope}:{sessionId}";
    private string BuildIndexKey() => $"{_settings.KeyPrefix}:index:{UserScope}";
}
