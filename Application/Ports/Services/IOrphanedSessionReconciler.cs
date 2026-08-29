namespace Application.Ports.Services;

/// <summary>
/// Cierra las sesiones que quedaron abiertas cuando el servicio dejó de estar disponible
/// (apagado programado, corte de luz, crash).
/// </summary>
public interface IOrphanedSessionReconciler
{
    /// <returns>Cuántas sesiones se cerraron.</returns>
    Task<int> ReconcileAsync(CancellationToken ct = default);
}
