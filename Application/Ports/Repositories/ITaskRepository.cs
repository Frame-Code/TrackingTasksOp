using Task = Domain.Entities.TrackingTasksEntities.Task;

namespace Application.Ports.Repositories;

public interface ITaskRepository : IRepository<Task>
{
    Task<Task?> GetByOpenProjectIdAsync(int id, bool tracking = false);
}