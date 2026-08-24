using System.Collections.Concurrent;

namespace Infrastructure.Adapters.Http;

/// <summary>
/// Serializa el refresh del access_token OAuth por usuario, entre requests distintos (no solo
/// dentro de uno). Doorkeeper rota el refresh_token al usarlo: si dos requests concurrentes
/// (cada uno con su propio DbContext scoped) ven el token vencido y refrescan a la vez, el
/// segundo falla porque el refresh_token que tenía ya quedó invalidado por el primero.
/// Singleton a propósito: el lock tiene que sobrevivir más allá de un único request.
/// </summary>
public class OAuthRefreshLock
{
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();

    public SemaphoreSlim GetLock(string userId) =>
        _locks.GetOrAdd(userId, _ => new SemaphoreSlim(1, 1));
}
