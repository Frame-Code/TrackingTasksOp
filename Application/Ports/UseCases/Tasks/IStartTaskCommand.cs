using Application.Dto.Tasks;
using TaskEntity = Domain.Entities.TrackingTasksEntities.Task;

namespace Application.Ports.UseCases.Tasks;

public interface IStartTaskCommand
{
    Task<TaskEntity> Execute(StarTaskRequest request);
}