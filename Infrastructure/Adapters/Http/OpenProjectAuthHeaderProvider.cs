using System.Net.Http.Headers;
using System.Text;
using Application.Ports.Auth;
using Application.Ports.Services;
using Domain.Entities.OpenProjectEntities.OAuth;
using Infrastructure.DataAccess;
using Infrastructure.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Adapters.Http;

/// <summary>
/// Resuelve UNA sola vez por request el header Authorization para OpenProject: Basic si el
/// usuario tiene LocalCredential (API key), Bearer si solo tiene OAuthCredential (refrescando
/// el access_token si venció). Local se prioriza cuando existen ambas — preserva el
/// comportamiento actual de cualquier usuario que ya tenía API key antes de que existiera OAuth.
///
/// El <see cref="TrackingTasksDbContext"/> es scoped y no admite operaciones concurrentes: si
/// un request dispara varias llamadas HTTP en paralelo y cada una consulta la credencial por su
/// cuenta, EF Core lanza "A second operation was started on this context instance before a
/// previous operation completed". Aquí la consulta se hace una vez y todas las llamadas esperan
/// el mismo Task.
///
/// Tiene que ser un servicio SCOPED aparte, no estado dentro del DelegatingHandler:
/// IHttpClientFactory reutiliza los handlers entre requests (y entre usuarios), así
/// que cachear la credencial ahí filtraría la API key/token de un usuario a otro.
/// </summary>
public class OpenProjectAuthHeaderProvider(
    TrackingTasksDbContext context,
    CurrentUser currentUser,
    IApiKeyEncryptorService encryptor,
    IOAuthService oAuthService,
    OAuthRefreshLock refreshLock,
    ILogger<OpenProjectAuthHeaderProvider> logger)
{
    // Ventana de gracia: refresca un poco antes de que venza en vez de justo al límite, para
    // no perder la carrera contra la llamada real a OpenProject que viene inmediatamente después.
    private static readonly TimeSpan ExpiryBuffer = TimeSpan.FromSeconds(30);

    private readonly object _gate = new();
    private Task<AuthenticationHeaderValue>? _header;

    public Task<AuthenticationHeaderValue> GetAuthorizationHeaderAsync()
    {
        if (_header is not null) return _header;

        lock (_gate)
        {
            _header ??= LoadAsync();
        }

        return _header;
    }

    private async Task<AuthenticationHeaderValue> LoadAsync()
    {
        var userId = currentUser.UserId
            ?? throw new UnauthorizedAccessException("Usuario no autenticado.");

        var localCredential = await context.Set<LocalCredential>()
            .FirstOrDefaultAsync(x => x.UserId == userId);
        if (localCredential is not null)
        {
            var apiKey = encryptor.UnProtect(localCredential.EncryptedApiKey);
            var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes($"apikey:{apiKey}"));
            return new AuthenticationHeaderValue("Basic", encoded);
        }

        var oAuthCredential = await context.Set<OAuthCredential>()
            .FirstOrDefaultAsync(x => x.UserId == userId)
            ?? throw new UnauthorizedAccessException($"El usuario {userId} no tiene una credencial de OpenProject configurada.");

        var accessToken = await ResolveOAuthAccessTokenAsync(oAuthCredential, userId);
        return new AuthenticationHeaderValue("Bearer", accessToken);
    }

    private async Task<string> ResolveOAuthAccessTokenAsync(OAuthCredential credential, string userId)
    {
        if (credential.OAuthTokenExpiresAt > DateTime.UtcNow.Add(ExpiryBuffer))
            return encryptor.UnProtect(credential.EncryptedOAuthAccessToken);

        var userLock = refreshLock.GetLock(userId);
        await userLock.WaitAsync();
        try
        {
            // Re-leer bajo el lock: mientras esperábamos, otro request (con su propio DbContext)
            // pudo haber refrescado ya. Reintentar acá con el refresh_token viejo fallaría, porque
            // Doorkeeper lo rota (invalida) al usarlo.
            var fresh = await context.Set<OAuthCredential>().AsNoTracking()
                .FirstAsync(x => x.UserId == userId);
            if (fresh.OAuthTokenExpiresAt > DateTime.UtcNow.Add(ExpiryBuffer))
                return encryptor.UnProtect(fresh.EncryptedOAuthAccessToken);

            if (string.IsNullOrEmpty(fresh.EncryptedOAuthRefreshToken))
                throw new UnauthorizedAccessException("La sesión de OpenProject expiró y no se puede renovar. Iniciá sesión de nuevo.");

            var instanceId = currentUser.OpenProjectInstanceId
                ?? throw new UnauthorizedAccessException("El usuario actual no tiene una instancia de OpenProject asociada.");

            Token newToken;
            try
            {
                var refreshToken = encryptor.UnProtect(fresh.EncryptedOAuthRefreshToken);
                newToken = await oAuthService.RefreshToken(refreshToken, instanceId);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to refresh OAuth token for user {UserId}", userId);
                throw new UnauthorizedAccessException("No se pudo renovar la sesión de OpenProject. Iniciá sesión de nuevo.");
            }

            fresh.EncryptedOAuthAccessToken = encryptor.Protect(newToken.AccessToken);
            if (!string.IsNullOrEmpty(newToken.RefreshToken))
                fresh.EncryptedOAuthRefreshToken = encryptor.Protect(newToken.RefreshToken);
            fresh.OAuthTokenExpiresAt = DateTime.UtcNow.AddSeconds((double)newToken.ExpiresIn);

            context.Set<OAuthCredential>().Update(fresh);
            await context.SaveChangesAsync();

            return newToken.AccessToken;
        }
        finally
        {
            userLock.Release();
        }
    }
}
