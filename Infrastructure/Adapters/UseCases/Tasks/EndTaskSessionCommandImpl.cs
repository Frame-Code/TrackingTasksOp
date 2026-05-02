using Application.Dto.Tasks;
using Application.Dto.TimeEntry;
using Application.Ports.Repositories;
using Application.Ports.UseCases.Tasks;
using Application.Ports.UseCases.TimeEntry;
using Application.Ports.UseCases.WorkPackages;
using Infrastructure.Adapters.Services;
using Task = Domain.Entities.TrackingTasksEntities.Task;

namespace Infrastructure.Adapters.UseCases.Tasks;

public class EndTaskSessionCommandImpl
    (ITaskRepository repository, IAddTimeEntry addTimeEntry, IUpdateWorkPackageCommand updateWorkPackageCommand): IEndTaskSessionCommand
{
    public async Task<Task> Execute(EndTaskSessionRequest request)
    {
        var task = await repository.GetByIdAsync(request.WorkPackageId)
            ?? throw new ArgumentException($"Task with OpenProjectId {request.WorkPackageId} does not exist");

        var lastTimeDetails = task.TasksTimeDetails.OrderBy(x => x.StartTime).LastOrDefault()
            ?? throw new InvalidOperationException($"Task with OpenProjectId {request.WorkPackageId} haven't any details");

        if (lastTimeDetails.EndTime == null)
        {
            lastTimeDetails.EndTime = DateTime.Now;
            
            //Agregando más tiempo de holgura ._. (Lógica de main)
            var time = lastTimeDetails.GetHoursWorked()!.Value.Minutes;
            if (time is >= 10 and <= 60)
                lastTimeDetails.EndTime = DateTime.Now.AddMinutes(TimeTrackService.GetRandomMinutes(10, 20));
            else if (time >= 60)
                lastTimeDetails.EndTime = DateTime.Now.AddMinutes(TimeTrackService.GetRandomMinutes(20, 40));
        }
        
        var timeEntryRequest = new AddTimeEntryRequest(request.WorkPackageId, request.ActivityId,
            lastTimeDetails.GetHoursWorked()!.Value.TotalHours, request.Comment);
        
        await addTimeEntry.Execute(timeEntryRequest);
        lastTimeDetails.Uploaded = true;

        if (request.NewStatusId.HasValue && request.NewStatusId.Value > 0)
        {
            await updateWorkPackageCommand.Execute(request.WorkPackageId, statusId: request.NewStatusId.Value);
            task.StatusTaskId = request.NewStatusId.Value;
        }

        return await repository.SaveAsync(task);
    }
}
