namespace Application.Ports.Services;

/// <summary>
/// Controla el uso de la key de IA COMPARTIDA del servidor. Un usuario con su propia API key
/// configurada (BYOK) no pasa por este límite — el costo y la cuota corren por su cuenta.
/// </summary>
public interface IAiUsageLimiter
{
    /// <summary>
    /// Intenta consumir un uso del bot para el usuario. Devuelve false si ya alcanzó el
    /// límite diario de la key compartida (y no tiene su propia key configurada).
    /// </summary>
    Task<bool> TryConsumeAsync(string userId, CancellationToken ct = default);
}
