using Application.Dto.Tasks;
using Application.Ports.Auth;
using Application.Ports.Repositories;
using Application.Ports.Services;
using Application.Ports.UseCases.Tasks;
using Application.Ports.UseCases.WorkPackages;
using TaskEntity = Domain.Entities.TrackingTasksEntities.Task;

namespace Infrastructure.Adapters.UseCases.Tasks;

public class PauseTaskCommandImpl(
    ITaskRepository repository,
    IUpdateWorkPackageCommand updateWorkPackageCommand,
    IPendingTimeUploader pendingTimeUploader,
    CurrentUser currentUser) : IPauseTaskCommand
{
    public async Task<TaskEntity> Execute(PauseTaskRequest request)
    {
        var userId = currentUser.UserId
            ?? throw new UnauthorizedAccessException("Usuario no autenticado.");

        var task = await repository.GetByIdForUserAsync(request.WorkPackageId, userId)
            ?? throw new ArgumentException($"Task with OpenProjectId {request.WorkPackageId} does not exist");

        var lastDetail = task.GetActiveSession()
            ?? throw new InvalidOperationException($"Task with OpenProjectId {request.WorkPackageId} doesn't have an active session to pause.");

        lastDetail.EndTime = DateTime.Now;

        // Subimos TODAS las sesiones cerradas aún sin registrar (la recién pausada y las que
        // hayan quedado de ciclos anteriores). Si el usuario eligió guardar solo en local, quedan
        // con Uploaded = false y se suben al finalizar la tarea o desde "Subir pendientes".
        if (request.UploadNow)
        {
            await pendingTimeUploader.UploadPendingAsync(task);
        }

        if (request.OnHoldStatusId.HasValue && request.OnHoldStatusId.Value > 0)
        {
            await updateWorkPackageCommand.Execute(request.WorkPackageId, statusId: request.OnHoldStatusId.Value);
            task.StatusTaskId = request.OnHoldStatusId.Value;
        }

        return await repository.SaveAsync(task);
    }

}
