using Application.Dto.Tasks;
using Application.Dto.TimeEntry;
using Application.Ports.Repositories;
using Application.Ports.UseCases.Tasks;
using Application.Ports.UseCases.TimeEntry;
using Domain.Entities.TrackingTasksEntities;
using Web.Infrastructure.Adapters.Services;
using TaskEntity = Domain.Entities.TrackingTasksEntities.Task;

namespace Web.Infrastructure.Adapters.UseCases.Tasks;

public class StartTaskCommandImpl(ITaskRepository repository, IAddTimeEntry addTimeEntry) : IStartTaskCommand
{
    public async Task<TaskEntity> Execute(StarTaskRequest request)
    {
        var task = await repository.GetByIdAsync(request.WorkPackageId) 
            ?? new TaskEntity
            {
                WorkPackageId = request.WorkPackageId,
                Name = request.Name,
                Description = request.Description,
                ProjectId = request.ProjectId,
                StatusTaskId = request.StatusId
            };

        //Cerrar la última entrada si es una nueva tarea
        var details = task.TasksTimeDetails.ToList();
        var lastDetail = details
            .OrderBy(x => x.StartTime)
            .LastOrDefault();

        if (lastDetail is not null && !lastDetail.EndTime.HasValue)
        {
            if (request.ActivityId is null)
                throw new ArgumentNullException($"No se puede cerrar entrada de tiempo sin una actividad asignada");
            
            lastDetail.EndTime = DateTime.Now;
            
            //Agregando más tiempo de holgura ._.
            var time = lastDetail.GetHoursWorked()!.Value.Minutes;
            if (time is >= 10 and <= 60)
                lastDetail.EndTime = DateTime.Now.AddMinutes(TimeTrackService.GetRandomMinutes(10, 20));
            else if (time >= 60)
                lastDetail.EndTime = DateTime.Now.AddMinutes(TimeTrackService.GetRandomMinutes(20, 40));
            
            var timeEntryRequest = new AddTimeEntryRequest(request.WorkPackageId, request.ActivityId ?? -1,
                lastDetail.GetHoursWorked()!.Value.TotalHours, request.Comment ?? string.Empty);
            
            await addTimeEntry.Execute(timeEntryRequest);
            lastDetail.Uploaded = true;
        }
        
        //Crear la nueva entrada de tiempo
        var detail = new TaskTimeDetail
        {
            IdTask = request.WorkPackageId
        };
        
        details.Add(detail);
        task.TasksTimeDetails = details;
        return await repository.SaveAsync(task);
    }
}