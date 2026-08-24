using Application.Ports.Auth;
using Application.Ports.Services;
using Application.Ports.UseCases.Auth;
using Infrastructure.DataAccess;
using Infrastructure.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Adapters.UseCases.Auth;

/// <summary>
/// Se ejecuta en logout. Si el usuario tiene OAuthCredential, revoca el access_token en
/// OpenProject (best-effort — si la instancia no responde, igual seguimos con el logout local)
/// y borra la credencial: un access_token revocado no sirve para nada más, y así el próximo
/// login por OAuth arranca limpio en vez de intentar reusar tokens muertos.
/// </summary>
public class RevokeOAuthSessionCommandImpl(
    TrackingTasksDbContext context,
    CurrentUser currentUser,
    IOAuthService oAuthService,
    IApiKeyEncryptorService encryptor,
    ILogger<RevokeOAuthSessionCommandImpl> logger) : IRevokeOAuthSessionCommand
{
    public async Task Execute(CancellationToken ct = default)
    {
        var userId = currentUser.UserId;
        if (userId is null) return;

        var credential = await context.Set<OAuthCredential>().FirstOrDefaultAsync(x => x.UserId == userId, ct);
        if (credential is null) return;

        var instanceId = currentUser.OpenProjectInstanceId;
        if (instanceId is not null)
        {
            try
            {
                var accessToken = encryptor.UnProtect(credential.EncryptedOAuthAccessToken);
                await oAuthService.RevokeToken(accessToken, instanceId.Value);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to revoke OAuth token for user {UserId} on logout", userId);
            }
        }

        context.Set<OAuthCredential>().Remove(credential);
        await context.SaveChangesAsync(ct);
    }
}
