using Application.Ports.Auth;
using Application.Ports.UseCases.Tasks;
using Infrastructure.DataAccess;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Adapters.UseCases.Tasks;

public class CancelTaskSessionCommandImpl(
    TrackingTasksDbContext context,
    CurrentUser currentUser,
    ILogger<CancelTaskSessionCommandImpl> logger) : ICancelTaskSessionCommand
{
    public async Task<bool> Execute(int workPackageId)
    {
        var userId = currentUser.UserId
            ?? throw new UnauthorizedAccessException("Usuario no autenticado.");

        logger.LogInformation("Cancelling active session for work package {WpId}", workPackageId);

        // Scope por usuario: sin esto, dos tenants distintos con un TaskTimeDetail para el
        // mismo WorkPackageId numérico podían cancelarse la sesión activa entre ellos.
        var openDetail = await context.TasksTimeDetails
            .FirstOrDefaultAsync(d => d.IdTask == workPackageId && d.UserId == userId && d.EndTime == null);

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
