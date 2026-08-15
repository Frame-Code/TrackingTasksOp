using TaskEntity = Domain.Entities.TrackingTasksEntities.Task;

namespace Application.Ports.Services;

/// <summary>
/// Sube a OpenProject las sesiones ya cerradas que quedaron guardadas solo en local
/// (Uploaded = false). Lo comparten pausar, finalizar y la subida manual, que antes
/// resolvían esto por separado y dejaban sesiones sin registrar nunca.
/// </summary>
public interface IPendingTimeUploader
{
    /// <summary>
    /// Registra en OpenProject todas las sesiones pendientes de la tarea y devuelve cuántas subió.
    /// Persiste después de cada entrada: si una falla, las ya subidas quedan marcadas y no se duplican al reintentar.
    /// </summary>
    Task<int> UploadPendingAsync(TaskEntity task, int? activityId = null, string comment = "", CancellationToken ct = default);
}
