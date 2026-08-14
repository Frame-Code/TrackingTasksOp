using Task = Domain.Entities.TrackingTasksEntities.Task;

namespace Application.Ports.Repositories;

public interface ITaskRepository : IRepository<Task>
{
    /// <summary>
    /// Devuelve la tarea del usuario que tiene una sesión de tiempo abierta (sin EndTime),
    /// o null si no hay ninguna. Invariante: como máximo una por usuario.
    /// </summary>
    System.Threading.Tasks.Task<Task?> GetActiveByUserAsync(string userId);
}