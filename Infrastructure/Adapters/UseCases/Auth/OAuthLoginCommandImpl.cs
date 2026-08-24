using Application.Dto;
using Application.Dto.Auth;
using Application.Ports.Auth;
using Application.Ports.Services;
using Application.Ports.UseCases.Auth;
using Infrastructure.DataAccess;
using Infrastructure.DataAccess.Entities;
using Infrastructure.DataAccess.Entities.Enums;
using Infrastructure.Exceptions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Adapters.UseCases.Auth;

public class OAuthLoginCommandImpl(
    IOAuthService oAuthService,
    IApiKeyEncryptorService apiKeyEncryptor,
    IAuthAuditLogger logger,
    TrackingTasksDbContext context,
    UserManager<ApplicationUser> userManager,
    ILogger<OAuthLoginCommandImpl> log
    ) : IOAuthLoginCommand
{
    public async Task<ResponseDto<AuthenticatedUserResponse>> ExecuteAsync(string code, string state, CancellationToken ct)
    {
        var (opUser, token, instanceId) = await oAuthService.OAuthCallback(code, state);

        var instance = await context.OpenProjectInstances.FirstOrDefaultAsync(x => x.Id == instanceId, ct)
            ?? throw new OpInstanceNotFoundException("Op Instance Not Found Or Not available");

        await context.Database.BeginTransactionAsync(ct);
        try
        {
            var appUser = await userManager.Users.FirstOrDefaultAsync(
                u => u.OpenProjectUserId == opUser.Id && u.OpenProjectInstanceId == instanceId, ct);

            var isNewUser = appUser is null;
            if (appUser is null)
            {
                appUser = new ApplicationUser
                {
                    Email = opUser.Email,
                    UserName = opUser.Email,
                    OpenProjectUserId = opUser.Id,
                    OpenProjectInstanceId = instance.Id,
                    OpenProjectInstanceBaseUrl = instance.BaseUrl,
                    AuthMethod = AuthMethod.OAuth
                };
                var createResult = await userManager.CreateAsync(appUser);
                if (createResult is { Succeeded: false })
                {
                    await context.Database.RollbackTransactionAsync(ct);
                    return new ResponseDto<AuthenticatedUserResponse>
                    {
                        IsSuccess = false,
                        ErrorMessage = string.Join("; ", createResult.Errors.Select(e => e.Description))
                    };
                }
            }

            var credential = await context.OAuthCredentials.FirstOrDefaultAsync(c => c.UserId == appUser.Id, ct);
            if (credential is null)
            {
                // Solo el FK escalar: setear también la navegación ApplicationUser hace que
                // EF, bajo NoTracking global, trate a appUser como una entidad nueva a insertar
                // (aunque ya exista o ya esté trackeado desde CreateAsync) y choque con la PK.
                credential = new OAuthCredential { UserId = appUser.Id };
                context.OAuthCredentials.Add(credential);
            }

            credential.EncryptedOAuthAccessToken = apiKeyEncryptor.Protect(token.AccessToken);
            credential.EncryptedOAuthRefreshToken = string.IsNullOrEmpty(token.RefreshToken)
                ? null
                : apiKeyEncryptor.Protect(token.RefreshToken);
            credential.OAuthTokenExpiresAt = DateTime.UtcNow.AddSeconds((double)token.ExpiresIn);
            credential.OAuthScope = token.Scope ?? "";

            await context.SaveChangesAsync(ct);
            await context.Database.CommitTransactionAsync(ct);

            await logger.LogAsync(
                isNewUser ? AuditEventType.OAuthGranted : AuditEventType.Login,
                appUser.Id,
                new { opUser.Email, instance.BaseUrl },
                ct);

            return new ResponseDto<AuthenticatedUserResponse>
            {
                IsSuccess = true,
                Data = new AuthenticatedUserResponse
                {
                    UserId = appUser.Id,
                    OpenProjectInstanceUrl = instance.BaseUrl,
                    Email = appUser.Email!,
                    Name = opUser.Name,
                    OpenProjectInstanceId = instance.Id
                }
            };
        }
        catch (Exception ex)
        {
            await context.Database.RollbackTransactionAsync(ct);
            log.LogError(ex, "Failed executing use case OAuthLoginCommandImpl, Message {Message}", ex.Message);
            throw;
        }
    }
}
