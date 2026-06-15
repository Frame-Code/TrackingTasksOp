using Application.Dto.Tasks;
using TaskEntity = Domain.Entities.TrackingTasksEntities.Task;

namespace Application.Ports.UseCases.Tasks;

public interface IPauseTaskCommand
{
    Task<TaskEntity> Execute(PauseTaskRequest request);
}
