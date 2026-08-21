using Application.Dto.OpInstance;
using Application.Ports.Auth;
using Application.Ports.Services;
using Infrastructure.DataAccess.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Web.Controllers.Dto.HttpRequest;

namespace Web.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class OpInstanceController(
    IUserOpService userOpService,
    IOpInstanceService opInstanceService,
    IApiKeyEncryptorService apiKeyEncryptor,
    UserManager<ApplicationUser> userManager,
    CurrentUser currentUser
    ) : ControllerBase 
{
    [HttpPost]
    public async Task<IActionResult> Save(OpInstanceHttpRequest request)
    {
        var userId = currentUser.UserId  ?? throw new UnauthorizedAccessException("Usuario no autenticado.");
        var appUser = await userManager.FindByIdAsync(userId) ?? throw new ApplicationException($"User {userId} not found while loading settings");
        
        var isAdmin = await userOpService.IsAdmin(appUser.OpenProjectUserId);
        if (!isAdmin)
            return new ForbidResult();

        var cipher = apiKeyEncryptor.Protect(request.ClientSecret);
        var dto = new OpInstanceDto(appUser.OpenProjectInstanceId, request.Alias, request.ClientId, cipher);
        await opInstanceService.save(dto);
        return Ok();
    }
}