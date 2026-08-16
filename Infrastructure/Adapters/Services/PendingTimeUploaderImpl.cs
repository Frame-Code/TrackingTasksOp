using Application.Dto.TimeEntry;
using Application.Ports.Repositories;
using Application.Ports.Services;
using Application.Ports.UseCases.TimeEntry;
using TaskEntity = Domain.Entities.TrackingTasksEntities.Task;

namespace Infrastructure.Adapters.Services;

public class PendingTimeUploaderImpl(
    ITaskRepository repository,
    IAddTimeEntryCommand addTimeEntryCommand,
    IActivityOpService activityOpService) : IPendingTimeUploader
{
    public async Task<int> UploadPendingAsync(TaskEntity task, int? activityId = null, string comment = "", CancellationToken ct = default)
    {
        var pending = task.TasksTimeDetails
            .Where(d => !d.Uploaded && d.EndTime != null && d.GetHoursWorked() is { TotalHours: > 0 })
            .OrderBy(d => d.StartTime)
            .ToList();

        if (pending.Count == 0) return 0;

        var resolvedActivityId = activityId is > 0
            ? activityId.Value
            : await ResolveDefaultActivityId(task.WorkPackageId);

        var uploaded = 0;
        foreach (var detail in pending)
        {
            await addTimeEntryCommand.Execute(new AddTimeEntryRequest(
                task.WorkPackageId,
                resolvedActivityId,
                detail.GetHoursWorked()!.Value.TotalHours,
                comment,
                DateOnly.FromDateTime(detail.StartTime)));

            // Se guarda entrada por entrada a propósito: si la siguiente falla, lo ya registrado
            // en OpenProject queda marcado y un reintento no lo vuelve a subir.
            detail.Uploaded = true;
            await repository.SaveAsync(task);
            uploaded++;
        }

        return uploaded;
    }

    private async Task<int> ResolveDefaultActivityId(int workPackageId)
    {
        var activities = await activityOpService.Lists(workPackageId);
        var defaultActivity = activities.FirstOrDefault()
            ?? throw new Exception($"No se encontró ninguna actividad disponible para registrar el tiempo de la tarea #{workPackageId}.");

        return defaultActivity.Id;
    }
}
