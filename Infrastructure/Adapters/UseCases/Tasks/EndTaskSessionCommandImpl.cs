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
    (ITaskRepository repository, IPendingTimeUploader pendingTimeUploader, IUpdateWorkPackageCommand updateWorkPackageCommand): IEndTaskSessionCommand
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

        // Se suben TODAS las sesiones cerradas sin registrar, no solo la última: al pausar
        // eligiendo "guardar en local" quedan sesiones con Uploaded = false, y antes nadie las
        // subía nunca, así que ese tiempo no llegaba a OpenProject.
        // Las ya subidas se saltan solas, de modo que no se duplican.
        await pendingTimeUploader.UploadPendingAsync(task, request.ActivityId, request.Comment);

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
