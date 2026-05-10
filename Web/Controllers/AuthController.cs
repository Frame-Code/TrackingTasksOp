using Application.Dto.Auth;
using Application.Ports.Services;
using Application.Ports.UseCases.Auth;
using Infrastructure.DataAccess.Entities;
using Infrastructure.Settings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Web.Controllers.Dto.HttpRequest;

namespace Web.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class AuthController(
    IRegisterLocalUserCommand registerLocalUserCommand,
    [FromKeyedServices(KeyedServicesNames.OpenProjectUrlService)]
    BaseUrlService urlService,
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager) : ControllerBase
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
            ?? throw new ApplicationException($"User with email {response.Data.Email} not found");
        
        await signInManager.SignInAsync(appUser, isPersistent: true);
        return Ok(new 
        {
            userId = appUser.Id,
            email = appUser.Email,
            openProjectInstanceUrl = response.Data!.OpenProjectInstanceUrl
        });
    }
}