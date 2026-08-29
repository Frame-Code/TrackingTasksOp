using Application.Ports.Services;
using Domain.Entities.TrackingTasksEntities;
using Infrastructure.DataAccess;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Adapters.Services;

/// <summary>
/// Corre al arrancar la app. Toda sesión que siga abierta en ese momento quedó huérfana: el
/// proceso anterior se detuvo sin que el usuario la cerrara.
///
/// Hace falta además del cierre perezoso de <c>StartTaskCommandImpl</c> porque una sesión de una
/// tarea que el usuario no vuelve a iniciar quedaría abierta para siempre, invisible: la cola de
/// pendientes filtra por <c>EndTime != null</c>, así que nadie se enteraría de que existe.
/// </summary>
public class OrphanedSessionReconcilerImpl(
    TrackingTasksDbContext context,
    ILogger<OrphanedSessionReconcilerImpl> logger) : IOrphanedSessionReconciler
{
    public async Task<int> ReconcileAsync(CancellationToken ct = default)
    {
        var orphaned = await context.Set<TaskTimeDetail>()
            .Where(d => d.EndTime == null)
            .ToListAsync(ct);

        if (orphaned.Count == 0) return 0;

        foreach (var detail in orphaned)
        {
            detail.CloseAsUnconfirmed();
            logger.LogInformation(
                "Sesión huérfana {DetailId} (usuario {UserId}, tarea {TaskId}) cerrada en {EndTime}; " +
                "iniciada {StartTime}, último latido {LastHeartbeat}. Queda pendiente de confirmación.",
                detail.Id, detail.UserId, detail.IdTask, detail.EndTime, detail.StartTime, detail.LastHeartbeat);
        }

        context.Set<TaskTimeDetail>().UpdateRange(orphaned);
        await context.SaveChangesAsync(ct);

        logger.LogWarning("Se cerraron {Count} sesiones que quedaron abiertas al detenerse el servicio.", orphaned.Count);
        return orphaned.Count;
    }
}
