using Application.Dto.Tasks;
using Application.Dto.TimeEntry;
using Application.Ports.Repositories;
using Application.Ports.UseCases.Tasks;
using Application.Ports.UseCases.TimeEntry;
using Task = Domain.Entities.TrackingTasksEntities.Task;

namespace Web.Infrastructure.Adapters.UseCases.Tasks;

public class EndTaskSessionCommandImpl
    (ITaskRepository repository, IAddTimeEntry addTimeEntry): IEndTaskSessionCommand
{
    public async Task<Task> Execute(EndTaskSessionRequest request)
    {
        var task = await repository.GetByIdAsync(request.WorkPackageId)
            ?? throw new ArgumentException($"Task with OpenProjectId {request.WorkPackageId} does not exist");

        var lastTimeDetails = task.TasksTimeDetails.OrderBy(x => x.StartTime).LastOrDefault()
            ?? throw new InvalidOperationException($"Task with OpenProjectId {request.WorkPackageId} haven't any details");

        lastTimeDetails.EndTime = DateTime.Now;
        var timeEntryRequest = new AddTimeEntryRequest(request.WorkPackageId, request.ActivityId,
            lastTimeDetails.GetHoursWorked()!.Value.TotalHours, request.Comment);
        
        await addTimeEntry.Execute(timeEntryRequest);
        lastTimeDetails.Uploaded = true;
        return await repository.SaveAsync(task);
    }
}