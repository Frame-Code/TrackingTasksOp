using Application.Ports.UseCases.Tasks;
using Infrastructure.DataAccess;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Adapters.UseCases.Tasks;

public class CancelTaskSessionCommandImpl(
    TrackingTasksDbContext context,
    ILogger<CancelTaskSessionCommandImpl> logger) : ICancelTaskSessionCommand
{
    public async Task<bool> Execute(int workPackageId)
    {
        logger.LogInformation("Cancelling active session for work package {WpId}", workPackageId);

        var openDetail = await context.TasksTimeDetails
            .FirstOrDefaultAsync(d => d.IdTask == workPackageId && d.EndTime == null);

        if (openDetail is null)
        {
            logger.LogWarning("No active session found for work package {WpId}", workPackageId);
            return false;
        }

        context.TasksTimeDetails.Remove(openDetail);
        await context.SaveChangesAsync();

        logger.LogInformation("Session cancelled for work package {WpId}, detail Id {DetailId}",
            workPackageId, openDetail.Id);

        return true;
    }
}
