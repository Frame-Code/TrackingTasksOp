using Task = Domain.Entities.TrackingTasksEntities.Task;

namespace Application.Ports.UseCases.Tasks;

public interface IEndTaskCommand
{
    Task<Task> Execute(Task request);
}