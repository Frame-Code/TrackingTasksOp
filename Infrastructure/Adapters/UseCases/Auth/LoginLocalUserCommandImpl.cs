using Application.Dto;
using Application.Dto.Auth;
using Application.Ports.Auth;
using Application.Ports.UseCases.Auth;
using Infrastructure.DataAccess;
using Infrastructure.DataAccess.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Adapters.UseCases.Auth;

public class LoginLocalUserCommandImpl(
    TrackingTasksDbContext context,
    IAuthAuditLogger logger,
    ILogger<LoginLocalUserCommandImpl> log,
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager) : ILoginLocalUserCommand
{
    public async Task<ResponseDto<AuthenticatedUserResponse>> ExecuteAsync(LocalLoginRequest request, CancellationToken ct)
    {
        try
        {
            var appUser = await userManager.FindByEmailAsync(request.Email);
            if (appUser is null)
            {
                return new ResponseDto<AuthenticatedUserResponse>
                {
                    IsSuccess = false,
                    ErrorMessage = "Credenciales inválidas"
                };
            }

            var result = await signInManager.CheckPasswordSignInAsync(appUser, request.Password, lockoutOnFailure: true);
            if (!result.Succeeded)
            {
                return new ResponseDto<AuthenticatedUserResponse>
                {
                    IsSuccess = false,
                    ErrorMessage = "Credenciales inválidas"
                };
            }

            var instance = await context.OpenProjectInstances.FirstOrDefaultAsync(x => x.Id == appUser.OpenProjectInstanceId, ct)
                           ?? throw new InvalidOperationException($"Can't find open project instance related with the current user");

            log.LogInformation("Logging user successfully");
            await logger.LogAsync(AuditEventType.Login, appUser.Id, new { request.Email, instance.BaseUrl }, ct);
            return new ResponseDto<AuthenticatedUserResponse>
            {
                IsSuccess = true,
                Data = new AuthenticatedUserResponse
                {
                    OpenProjectInstanceId = instance.Id,
                    Email = appUser.Email!,
                    Name = appUser.UserName!,
                    OpenProjectInstanceUrl = instance.BaseUrl,
                    UserId = appUser.Id
                }
            };
        }
        catch (Exception e)
        {
            log.LogError(e, "Failed executing use case LoginLocalUserCommandImpl, Message {Message}", e.Message);
            throw;
        }
    }
}