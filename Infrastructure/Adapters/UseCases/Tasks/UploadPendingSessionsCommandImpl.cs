using Application.Ports.Repositories;
using Application.Ports.Services;
using Application.Ports.UseCases.Tasks;

namespace Infrastructure.Adapters.UseCases.Tasks;

public class UploadPendingSessionsCommandImpl(
    ITaskRepository repository,
    IPendingTimeUploader pendingTimeUploader) : IUploadPendingSessionsCommand
{
    public async Task<int> Execute(int workPackageId, CancellationToken ct = default)
    {
        var task = await repository.GetByIdAsync(workPackageId)
            ?? throw new ArgumentException($"Task with OpenProjectId {workPackageId} does not exist");

        return await pendingTimeUploader.UploadPendingAsync(task, ct: ct);
    }
}
