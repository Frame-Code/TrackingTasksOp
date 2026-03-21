using Application.Dto.Tasks;
using Task = Domain.Entities.TrackingTasksEntities.Task;

namespace Application.Ports.UseCases.Tasks;

public interface IStartTaskCommand
{
    Task<System.Threading.Tasks.Task> Execute(StarTaskRequest request);
}