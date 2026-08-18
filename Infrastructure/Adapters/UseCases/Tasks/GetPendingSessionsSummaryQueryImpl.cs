using Application.Dto.Tasks;
using Application.Ports.Auth;
using Application.Ports.Repositories;
using Application.Ports.UseCases.Tasks;

namespace Infrastructure.Adapters.UseCases.Tasks;

public class GetPendingSessionsSummaryQueryImpl(
    ITaskRepository repository,
    CurrentUser currentUser) : IGetPendingSessionsSummaryQuery
{
    public async Task<PendingSessionsSummaryResponse> Execute(CancellationToken ct = default)
    {
        var userId = currentUser.UserId;
        if (string.IsNullOrEmpty(userId))
            throw new UnauthorizedAccessException("Usuario no autenticado.");

        var tasks = await repository.GetAllAsync(t =>
            t.UserId == userId && t.TasksTimeDetails.Any(d => !d.Uploaded && d.EndTime != null));

        var pendingDetails = tasks
            .SelectMany(t => t.TasksTimeDetails)
            .Where(d => !d.Uploaded && d.GetHoursWorked() is { TotalHours: > 0 })
            .ToList();

        var totalHours = pendingDetails.Sum(d => d.GetHoursWorked()!.Value.TotalHours);

        return new PendingSessionsSummaryResponse(pendingDetails.Count, Math.Round(totalHours, 2));
    }
}
