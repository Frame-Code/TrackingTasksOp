using Application.Dto.Tasks;
using Application.Ports.Repositories;
using Application.Ports.UseCases.Tasks;
using Application.Ports.UseCases.WorkPackages;
using TaskEntity = Domain.Entities.TrackingTasksEntities.Task;

namespace Infrastructure.Adapters.UseCases.Tasks;

public class PauseTaskCommandImpl(
    ITaskRepository repository,
    IUpdateWorkPackageCommand updateWorkPackageCommand) : IPauseTaskCommand
{
    public async Task<TaskEntity> Execute(PauseTaskRequest request)
    {
        var task = await repository.GetByIdAsync(request.WorkPackageId)
            ?? throw new ArgumentException($"Task with OpenProjectId {request.WorkPackageId} does not exist");

        var lastDetail = task.TasksTimeDetails.OrderBy(x => x.StartTime).LastOrDefault();
        if (lastDetail is null || lastDetail.EndTime != null)
            throw new InvalidOperationException($"Task with OpenProjectId {request.WorkPackageId} doesn't have an active session to pause.");

        lastDetail.EndTime = DateTime.Now;
        lastDetail.Uploaded = false;

        if (request.OnHoldStatusId.HasValue && request.OnHoldStatusId.Value > 0)
        {
            await updateWorkPackageCommand.Execute(request.WorkPackageId, statusId: request.OnHoldStatusId.Value);
            task.StatusTaskId = request.OnHoldStatusId.Value;
        }

        return await repository.SaveAsync(task);
    }
}
