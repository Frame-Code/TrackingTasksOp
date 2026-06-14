using System.Text.Json;
using Application.Dto.Tasks;
using Application.Dto.TimeEntry;
using Application.Ports.Repositories;
using Application.Ports.Services;
using Application.Ports.UseCases.Tasks;
using Application.Ports.UseCases.TimeEntry;
using Application.Ports.UseCases.WorkPackages;
using Infrastructure.Adapters.Services;
using Infrastructure.Exceptions;
using Task = Domain.Entities.TrackingTasksEntities.Task;

namespace Infrastructure.Adapters.UseCases.Tasks;

public class EndTaskSessionCommandImpl
    (ITaskRepository repository, IAddTimeEntryCommand addTimeEntryCommand, IUpdateWorkPackageCommand updateWorkPackageCommand, IActivityOpService activityOpService): IEndTaskSessionCommand
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

        var activityId = request.ActivityId is > 0
            ? request.ActivityId.Value
            : await ResolveDefaultActivityId(request.WorkPackageId);

        var timeEntryRequest = new AddTimeEntryRequest(request.WorkPackageId, activityId,
            lastTimeDetails.GetHoursWorked()!.Value.TotalHours, request.Comment);

        await addTimeEntryCommand.Execute(timeEntryRequest);
        lastTimeDetails.Uploaded = true;

        // Persistimos el registro de tiempo de inmediato: si el cambio de estado falla
        // después, no debe perderse ni duplicarse al reintentar.
        task = await repository.SaveAsync(task);

        if (request.NewStatusId.HasValue && request.NewStatusId.Value > 0)
        {
            try
            {
                await updateWorkPackageCommand.Execute(request.WorkPackageId, statusId: request.NewStatusId.Value);
            }
            catch (InvalidStatusTransitionException ex)
            {
                throw new TaskStatusTransitionException(
                    $"El tiempo se registró correctamente, pero no se pudo cambiar el estado de la tarea #{request.WorkPackageId}: {ex.Message}{ex.BuildSuggestion()}");
            }
            catch (Exception ex)
            {
                throw new TaskStatusTransitionException(
                    $"El tiempo se registró correctamente, pero no se pudo cambiar el estado de la tarea #{request.WorkPackageId}: {ExtractOpenProjectErrorMessage(ex.Message)}");
            }

            task.StatusTaskId = request.NewStatusId.Value;
            task = await repository.SaveAsync(task);
        }

        return task;
    }

    private async Task<int> ResolveDefaultActivityId(int workPackageId)
    {
        var activities = await activityOpService.Lists(workPackageId);
        var defaultActivity = activities.FirstOrDefault()
            ?? throw new Exception($"No se encontró ninguna actividad disponible para registrar el tiempo de la tarea #{workPackageId}.");

        return defaultActivity.Id;
    }

    /// <summary>
    /// Los errores HTTP de OpenProject vienen como "Error HTTP 422: {json con _type, message, ...}".
    /// Extraemos el campo "message" del JSON para mostrar un texto legible al usuario.
    /// </summary>
    private static string ExtractOpenProjectErrorMessage(string exceptionMessage)
    {
        var jsonStart = exceptionMessage.IndexOf('{');
        if (jsonStart < 0) return exceptionMessage;

        try
        {
            using var json = JsonDocument.Parse(exceptionMessage[jsonStart..]);
            if (json.RootElement.TryGetProperty("message", out var msg))
                return msg.GetString() ?? exceptionMessage;
        }
        catch (JsonException)
        {
        }

        return exceptionMessage;
    }
}
