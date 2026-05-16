using System.Security.Claims;
using Application.Dto.Auth;
using Application.Ports.Services;
using Application.Ports.UseCases.Auth;
using Infrastructure.DataAccess.Entities;
using Infrastructure.Settings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Web.Controllers.Dto.HttpRequest;

namespace Web.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class AuthController(
    IRegisterLocalUserCommand registerLocalUserCommand,
    ILoginLocalUserCommand loginLocalUserCommand,
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    [FromKeyedServices(KeyedServicesNames.OpenProjectUrlService)]
    BaseUrlService urlService) : ControllerBase
{
    
    [HttpPost("local-register")]
    [AllowAnonymous]
    public async Task<IActionResult> LocalRegisterAsync(LocalRegisterHttpRequest request, CancellationToken ct)
    {
        var opUrlNormalized = urlService.NormalizeUrl(request.OpenProjectInstanceUrl);
        var isUrlValid  = urlService.Validate(opUrlNormalized, request.ValidateSemanticOpenProjectUrl);
        
        if (!isUrlValid)
        {
            return BadRequest(new
            {
                message = "Invalid Open Project Instance Url",
            });
        }

        var commandRequest = new LocalRegisterRequest
        {
            Email = request.Email,
            ApiKey = request.ApiKey,
            OpenProjectInstanceUrl = opUrlNormalized,
            Password = request.Password
        };
        var response = await registerLocalUserCommand.ExecuteAsync(commandRequest, ct);
        if (!response.IsSuccess)
        {
            return BadRequest(new
            {
                message = response.ErrorMessage,
            });
        }

        var appUser = await userManager.FindByEmailAsync(response.Data!.Email)
            ?? throw new ApplicationException($"User with email {response.Data.Email} not found after registration");
        await signInManager.SignInAsync(appUser, isPersistent: true);

        return Ok(new
        {
            userId = response.Data!.UserId,
            email = response.Data.Email,
            openProjectInstanceUrl = response.Data!.OpenProjectInstanceUrl
        });
    }
    
    [HttpPost("local-login")]
    [AllowAnonymous]
    public async Task<IActionResult> LocalLoginAsync(LocalLoginHttpRequest request, CancellationToken ct)
    {
        var commandRequest = new LocalLoginRequest { Email = request.Email, Password = request.Password };
        var response = await loginLocalUserCommand.ExecuteAsync(commandRequest, ct);

        if (!response.IsSuccess)
            return Unauthorized(new { message = response.ErrorMessage });

        var appUser = await userManager.FindByEmailAsync(response.Data!.Email)
            ?? throw new ApplicationException($"User with email {response.Data.Email} not found after login");

        var principal = await signInManager.CreateUserPrincipalAsync(appUser);
        var identity = principal.Identity! as ClaimsIdentity;
        identity?.AddClaim(new Claim("OpenProjectInstanceBaseUrl", appUser.OpenProjectInstanceBaseUrl));
        
        await userManager.AddClaimAsync(appUser,
            new Claim("UserName", appUser.UserName ?? "-"));
        await HttpContext.SignInAsync(IdentityConstants.ApplicationScheme, principal);

        return Ok(response.Data);
    }
}