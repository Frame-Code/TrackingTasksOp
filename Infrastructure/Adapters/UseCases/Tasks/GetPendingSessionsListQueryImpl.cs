using Application.Dto.Tasks;
using Application.Ports.Auth;
using Application.Ports.Repositories;
using Application.Ports.UseCases.Tasks;

namespace Infrastructure.Adapters.UseCases.Tasks;

public class GetPendingSessionsListQueryImpl(
    ITaskRepository taskRepository,
    IProjectRepository projectRepository,
    CurrentUser currentUser) : IGetPendingSessionsListQuery
{
    public async Task<List<PendingSessionTaskRow>> Execute(CancellationToken ct = default)
    {
        var userId = currentUser.UserId;
        if (string.IsNullOrEmpty(userId))
            throw new UnauthorizedAccessException("Usuario no autenticado.");

        var tasks = await taskRepository.GetAllAsync(t =>
            t.UserId == userId && t.TasksTimeDetails.Any(d => !d.Uploaded && d.EndTime != null));

        var instanceId = currentUser.OpenProjectInstanceId;
        var projectNames = (await projectRepository.GetAllAsync(p => p.OpenProjectInstanceId == instanceId))
            .ToDictionary(p => p.Id, p => p.Name);

        return tasks
            .Select(t => new PendingSessionTaskRow(
                t.WorkPackageId,
                t.Name,
                projectNames.GetValueOrDefault(t.ProjectId, "Desconocido"),
                Math.Round(t.TasksTimeDetails
                    .Where(d => !d.Uploaded && d.GetHoursWorked() is { TotalHours: > 0 })
                    .Sum(d => d.GetHoursWorked()!.Value.TotalHours), 2)))
            .Where(r => r.Hours > 0)
            .OrderBy(r => r.TaskName)
            .ToList();
    }
}
