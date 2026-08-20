using Task = Domain.Entities.TrackingTasksEntities.Task;

namespace Application.Ports.Repositories;

public interface ITaskRepository : IRepository<Task>
{
    /// <summary>
    /// Devuelve la tarea del usuario que tiene una sesión de tiempo abierta (sin EndTime),
    /// o null si no hay ninguna. Invariante: como máximo una por usuario.
    /// </summary>
    System.Threading.Tasks.Task<Task?> GetActiveByUserAsync(string userId);

    /// <summary>
    /// Busca la tarea por WorkPackageId, acotada al usuario dueño. La PK real es compuesta
    /// (UserId, WorkPackageId): buscar solo por WorkPackageId (como hace el GetByIdAsync
    /// heredado de IRepository&lt;T&gt;) puede devolver la tarea de OTRO usuario si dos
    /// tenants distintos trackean el mismo WorkPackageId numérico. Usar siempre este método
    /// en vez del genérico para cualquier operación que dependa de "la tarea del usuario actual".
    /// </summary>
    System.Threading.Tasks.Task<Task?> GetByIdForUserAsync(int workPackageId, string userId, bool tracking = false);
}