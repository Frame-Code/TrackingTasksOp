using Application.Dto;
using Application.Dto.Auth;
using Application.Ports.Auth;
using Application.Ports.UseCases.Auth;
using Domain.Entities.TrackingTasksEntities;
using Infrastructure.DataAccess;
using Infrastructure.DataAccess.Entities;
using Infrastructure.DataAccess.Entities.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Adapters.UseCases.Auth;

public class RegisterLocalUserCommandImpl(
    IApiKeyValidatorService apiKeyValidator,
    IApiKeyEncryptorService apiKeyEncryptor,
    ILogger<RegisterLocalUserCommandImpl> log,
    IAuthAuditLogger logger,
    TrackingTasksDbContext context,
    UserManager<ApplicationUser> userManager) : IRegisterLocalUserCommand
{
    public async Task<ResponseDto<AuthenticatedUserResponse>> ExecuteAsync(LocalRegisterRequest request, CancellationToken ct)
    {
        var userExists = await userManager.FindByEmailAsync(request.Email);
        if (userExists is not null)
            return new ResponseDto<AuthenticatedUserResponse>
            {
                IsSuccess = false,
                ErrorMessage = "El usuario ya se encuentra registrado"
            };
        
        var user = await apiKeyValidator.ValidateAsync(request.OpenProjectInstanceUrl, request.ApiKey, ct);
        await context.Database.BeginTransactionAsync(ct);
        try
        {
            var instance = await context.OpenProjectInstances.FirstOrDefaultAsync(x => x.BaseUrl == request.OpenProjectInstanceUrl, ct);
            if (instance is null)
            {
                instance = new OpenProjectInstance
                {
                    BaseUrl = request.OpenProjectInstanceUrl
                }; 
                context.OpenProjectInstances.Add(instance);
                await context.SaveChangesAsync(ct);
            }
            
            var appUser = new ApplicationUser
            {
                Email = request.Email,
                OpenProjectUserId = user.Id,
                OpenProjectInstanceId = instance.Id,
                UserName = request.Email,
                AuthMethod = AuthMethod.Local
            };
            var createResult = await userManager.CreateAsync(appUser, request.Password);
            if (createResult is { Succeeded: false })
            {
                await context.Database.RollbackTransactionAsync(ct);
                return new ResponseDto<AuthenticatedUserResponse>
                {
                    IsSuccess = false,
                    ErrorMessage = string.Join("; ", createResult.Errors.Select(e => e.Description))
                };
            }
            
            var cipher = apiKeyEncryptor.Protect(request.ApiKey);
            var credentialUser = new LocalCredential
            {
                UserId = appUser.Id,
                ApiKeyLastValidatedAt = DateTime.UtcNow,
                ApiKeyStatus = ApiKeyStatus.Valid,
                EncryptedApiKey = cipher,
                ApplicationUser = appUser
            };
            var logDetail = new
            {
                request.Email,
                OpInstanceUrl = request.OpenProjectInstanceUrl,
                AuthMethod = nameof(AuthMethod.Local),
                ApiKeyStatus = nameof(ApiKeyStatus.Valid)
            };
            
            context.Add(credentialUser);
            await context.SaveChangesAsync(ct);
            await context.Database.CommitTransactionAsync(ct);
            await logger.LogAsync(AuditEventType.Register, appUser.Id, logDetail, ct);
            
            return new ResponseDto<AuthenticatedUserResponse>
            {
                IsSuccess = true,
                Data = new AuthenticatedUserResponse
                {
                    UserId = appUser.Id,
                    OpenProjectInstanceUrl = request.OpenProjectInstanceUrl,
                    Email = request.Email,
                    Name = user.Name,
                    OpenProjectInstanceId = instance.Id
                }
            };
        }
        catch (Exception ex)
        {
            await context.Database.RollbackTransactionAsync(ct);
            log.LogError(ex, "Failed executing use case RegisterLocalUserCommandImpl, Message {Message}", ex.Message);
            throw;
        }
    }
}