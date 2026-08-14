using Application.Dto.Tasks;
using Application.Dto.TimeEntry;
using Application.Ports.Repositories;
using Application.Ports.Services;
using Application.Ports.UseCases.Tasks;
using Application.Ports.UseCases.TimeEntry;
using Application.Ports.UseCases.WorkPackages;
using TaskEntity = Domain.Entities.TrackingTasksEntities.Task;

namespace Infrastructure.Adapters.UseCases.Tasks;

public class PauseTaskCommandImpl(
    ITaskRepository repository,
    IUpdateWorkPackageCommand updateWorkPackageCommand,
    IAddTimeEntryCommand addTimeEntryCommand,
    IActivityOpService activityOpService) : IPauseTaskCommand
{
    public async Task<TaskEntity> Execute(PauseTaskRequest request)
    {
        var task = await repository.GetByIdAsync(request.WorkPackageId)
            ?? throw new ArgumentException($"Task with OpenProjectId {request.WorkPackageId} does not exist");

        var lastDetail = task.TasksTimeDetails.OrderBy(x => x.StartTime).LastOrDefault();
        if (lastDetail is null || lastDetail.EndTime != null)
            throw new InvalidOperationException($"Task with OpenProjectId {request.WorkPackageId} doesn't have an active session to pause.");

        lastDetail.EndTime = DateTime.Now;

        // Subimos a OpenProject TODAS las sesiones ya finalizadas que aún no se hayan registrado
        // (la que se acaba de pausar y cualquier otra pendiente de ciclos anteriores), para que
        // el tiempo invertido no se pierda al pausar. Si el usuario eligió guardar en local,
        // las dejamos con Uploaded = false y se subirán al finalizar la tarea.
        if (request.UploadNow)
        {
            var pendingDetails = task.TasksTimeDetails
                .Where(d => !d.Uploaded && d.EndTime != null && d.GetHoursWorked() is { TotalHours: > 0 })
                .ToList();

            if (pendingDetails.Count > 0)
            {
                var activityId = await ResolveDefaultActivityId(request.WorkPackageId);
                foreach (var detail in pendingDetails)
                {
                    await addTimeEntryCommand.Execute(new AddTimeEntryRequest(
                        request.WorkPackageId, activityId, detail.GetHoursWorked()!.Value.TotalHours, string.Empty,
                        DateOnly.FromDateTime(detail.StartTime)));
                    detail.Uploaded = true;
                }
            }
        }

        if (request.OnHoldStatusId.HasValue && request.OnHoldStatusId.Value > 0)
        {
            await updateWorkPackageCommand.Execute(request.WorkPackageId, statusId: request.OnHoldStatusId.Value);
            task.StatusTaskId = request.OnHoldStatusId.Value;
        }

        return await repository.SaveAsync(task);
    }

    private async Task<int> ResolveDefaultActivityId(int workPackageId)
    {
        var activities = await activityOpService.Lists(workPackageId);
        var defaultActivity = activities.FirstOrDefault()
            ?? throw new Exception($"No se encontró ninguna actividad disponible para registrar el tiempo de la tarea #{workPackageId}.");

        return defaultActivity.Id;
    }
}
