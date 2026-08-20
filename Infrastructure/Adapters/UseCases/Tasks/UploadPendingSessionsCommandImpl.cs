using Application.Ports.Auth;
using Application.Ports.Repositories;
using Application.Ports.Services;
using Application.Ports.UseCases.Tasks;

namespace Infrastructure.Adapters.UseCases.Tasks;

public class UploadPendingSessionsCommandImpl(
    ITaskRepository repository,
    IPendingTimeUploader pendingTimeUploader,
    CurrentUser currentUser) : IUploadPendingSessionsCommand
{
    public async Task<int> Execute(int workPackageId, CancellationToken ct = default)
    {
        var userId = currentUser.UserId
            ?? throw new UnauthorizedAccessException("Usuario no autenticado.");

        var task = await repository.GetByIdForUserAsync(workPackageId, userId)
            ?? throw new ArgumentException($"Task with OpenProjectId {workPackageId} does not exist");

        return await pendingTimeUploader.UploadPendingAsync(task, ct: ct);
    }
}
