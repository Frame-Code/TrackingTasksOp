using Application.Dto;
using Application.Dto.Auth;
using Application.Ports.Auth;
using Application.Ports.UseCases.Auth;
using Infrastructure.DataAccess;
using Infrastructure.DataAccess.Entities;
using Infrastructure.DataAccess.Entities.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Adapters.UseCases.Auth;

public class UpdateApiKeyCommandImpl(
    IApiKeyValidatorService apiKeyValidator,
    IApiKeyEncryptorService apiKeyEncryptor,
    IAuthAuditLogger logger,
    ILogger<UpdateApiKeyCommandImpl> log,
    TrackingTasksDbContext context,
    UserManager<ApplicationUser> userManager,
    CurrentUser currentUser) : IUpdateApiKeyCommand
{
    public async Task<ResponseDto<AuthenticatedUserResponse>> ExecuteAsync(UpdateApiKeyRequest request, CancellationToken ct)
    {
        var userId = currentUser.UserId
            ?? throw new UnauthorizedAccessException("Usuario no autenticado.");
        var instanceUrl = currentUser.OpenProjectInstanceUrl
            ?? throw new InvalidOperationException("El usuario autenticado no tiene una instancia de OpenProject asociada.");

        try
        {
            var credential = await context.Set<LocalCredential>().FirstOrDefaultAsync(x => x.UserId == userId, ct);
            if (credential is null)
            {
                return new ResponseDto<AuthenticatedUserResponse>
                {
                    IsSuccess = false,
                    ErrorMessage = "No se encontró una credencial local para este usuario."
                };
            }

            // Valida la API key nueva contra OpenProject ANTES de pisar la que ya está
            // guardada — si es inválida, ApiKeyValidatorServiceImpl tira InvalidApiKeyException
            // (ya mapeada a 400 en GlobalExceptionHandler) y la credencial vieja queda intacta.
            var opUser = await apiKeyValidator.ValidateAsync(instanceUrl, request.ApiKey, ct);

            credential.EncryptedApiKey = apiKeyEncryptor.Protect(request.ApiKey);
            credential.ApiKeyStatus = ApiKeyStatus.Valid;
            credential.ApiKeyLastValidatedAt = DateTime.UtcNow;

            // El DbContext usa QueryTrackingBehavior.NoTracking global (ver DbContextExtensions.cs),
            // así que "credential" no está trackeada: sin esto, SaveChangesAsync no detecta ningún
            // cambio y no emite el UPDATE — la API key vieja quedaría para siempre. Se marca solo
            // la entidad raíz como Modified (no context.Update(), que cascadea a la navegación
            // ApplicationUser y puede chocar con una instancia de ese usuario ya trackeada).
            context.Entry(credential).State = EntityState.Modified;
            await context.SaveChangesAsync(ct);

            var appUser = await userManager.FindByIdAsync(userId)
                ?? throw new ApplicationException($"User {userId} not found after updating API key");

            await logger.LogAsync(AuditEventType.ApiKeyChanged, userId, new { instanceUrl }, ct);
            log.LogInformation("API key actualizada correctamente para el usuario {UserId}", userId);

            return new ResponseDto<AuthenticatedUserResponse>
            {
                IsSuccess = true,
                Data = new AuthenticatedUserResponse
                {
                    UserId = appUser.Id,
                    Email = appUser.Email!,
                    Name = opUser.Name,
                    OpenProjectInstanceUrl = instanceUrl,
                    OpenProjectInstanceId = currentUser.OpenProjectInstanceId ?? 0
                }
            };
        }
        catch (Exception e)
        {
            log.LogError(e, "Failed executing use case UpdateApiKeyCommandImpl, Message {Message}", e.Message);
            throw;
        }
    }
}
