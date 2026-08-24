using Application.Dto.Auth;
using Application.Ports.Auth;
using Application.Ports.UseCases.Account;
using Infrastructure.DataAccess;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Adapters.UseCases.Account;

public class GetAvatarQueryImpl(
    TrackingTasksDbContext context,
    CurrentUser currentUser) : IGetAvatarQuery
{
    public async Task<AvatarResponse?> Execute(CancellationToken ct = default)
    {
        var userId = currentUser.UserId
            ?? throw new UnauthorizedAccessException("Usuario no autenticado.");

        return await context.UserAvatars
            .Where(a => a.UserId == userId)
            .Select(a => new AvatarResponse(a.Jpeg, a.UpdatedAt))
            .FirstOrDefaultAsync(ct);
    }
}
