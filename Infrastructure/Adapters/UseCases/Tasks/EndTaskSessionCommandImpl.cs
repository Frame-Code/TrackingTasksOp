using System.Text.Json;
using Application.Dto.Tasks;
using Application.Dto.TimeEntry;
using Application.Ports.Auth;
using Application.Ports.Repositories;
using Application.Ports.Services;
using Application.Ports.UseCases.Tasks;
using Application.Ports.UseCases.TimeEntry;
using Application.Ports.UseCases.WorkPackages;
using Infrastructure.Adapters.Services;
using Infrastructure.DataAccess.Entities;
using Infrastructure.Exceptions;
using Microsoft.AspNetCore.Identity;
using Task = Domain.Entities.TrackingTasksEntities.Task;

namespace Infrastructure.Adapters.UseCases.Tasks;

public class EndTaskSessionCommandImpl(
    ITaskRepository repository,
    IPendingTimeUploader pendingTimeUploader,
    IUpdateWorkPackageCommand updateWorkPackageCommand,
    UserManager<ApplicationUser> userManager,
    CurrentUser currentUser) : IEndTaskSessionCommand
{
    public async Task<Task> Execute(EndTaskSessionRequest request)
    {
        var userId = currentUser.UserId
            ?? throw new UnauthorizedAccessException("Usuario no autenticado.");

        var task = await repository.GetByIdForUserAsync(request.WorkPackageId, userId)
            ?? throw new ArgumentException($"Task with OpenProjectId {request.WorkPackageId} does not exist");

        if (!task.TasksTimeDetails.Any())
            throw new InvalidOperationException($"Task with OpenProjectId {request.WorkPackageId} haven't any details");

        var lastTimeDetails = task.GetActiveSession();

        if (lastTimeDetails is not null)
        {
            lastTimeDetails.EndTime = DateTime.Now;

            // Redondeo al cuarto de hora: parametrizado desde Ajustes ("Tareas").
            // Desactivado, se registran los minutos exactos trackeados.
            //
            // El resultado se calcula desde StartTime y no sumándole minutos a DateTime.Now:
            // el margen es una propiedad de cuánto duró la sesión, no de cuándo se cerró.
            if (await ShouldAddRandomSlackTime())
            {
                var tracked = lastTimeDetails.GetHoursWorked()!.Value;
                lastTimeDetails.EndTime = lastTimeDetails.StartTime + TimeTrackService.RoundUpToQuarterHour(tracked);
            }
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

    /// <summary>
    /// Default true: preserva el comportamiento histórico si el usuario no tiene la
    /// preferencia guardada o no se pudo resolver quién está autenticado.
    /// </summary>
    private async System.Threading.Tasks.Task<bool> ShouldAddRandomSlackTime()
    {
        var userId = currentUser.UserId;
        if (userId is null) return true;

        var appUser = await userManager.FindByIdAsync(userId);
        return appUser?.AddRandomSlackTime ?? true;
    }
}
