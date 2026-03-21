using Task = Domain.Entities.TrackingTasksEntities.Task;

namespace Application.Ports.Repositories;

public interface ITaskRepository : IRepository<Task>
{

}