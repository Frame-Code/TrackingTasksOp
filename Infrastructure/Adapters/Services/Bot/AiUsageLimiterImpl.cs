using Application.Ports.Services;
using Infrastructure.DataAccess.Entities;
using Infrastructure.Settings;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Infrastructure.Adapters.Services.Bot;

public class AiUsageLimiterImpl(
    IConnectionMultiplexer redis,
    IOptions<GroqSettings> groqSettings,
    UserManager<ApplicationUser> userManager,
    ILogger<AiUsageLimiterImpl> logger) : IAiUsageLimiter
{
    public async Task<bool> TryConsumeAsync(string userId, CancellationToken ct = default)
    {
        var appUser = await userManager.FindByIdAsync(userId);
        if (!string.IsNullOrEmpty(appUser?.EncryptedGroqApiKey))
            return true; // BYOK: sin límite, el costo/cuota corre por su cuenta.

        var limit = groqSettings.Value.DailyMessageLimitPerUser;
        if (limit <= 0) return true; // límite desactivado

        try
        {
            var db = redis.GetDatabase();
            var key = $"ai-usage:{userId}:{DateOnly.FromDateTime(DateTime.UtcNow):yyyy-MM-dd}";

            var count = await db.StringIncrementAsync(key);
            if (count == 1)
                await db.KeyExpireAsync(key, TimeSpan.FromHours(26)); // margen sobre las 24h del día

            return count <= limit;
        }
        catch (Exception ex)
        {
            // Si Redis no responde, no le negamos el bot a nadie por un problema de
            // infraestructura secundaria — se prioriza disponibilidad sobre el límite de costo.
            logger.LogWarning(ex, "No se pudo verificar el límite de uso de IA para {UserId}; se permite la solicitud", userId);
            return true;
        }
    }
}
