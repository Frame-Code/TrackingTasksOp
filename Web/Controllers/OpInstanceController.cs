using Application.Dto.OpInstance;
using Application.Ports.Auth;
using Application.Ports.Services;
using Infrastructure.DataAccess.Entities;
using Microsoft.AspNetCore.Authorization;
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
        var dto = new SaveOpInstanceDto(appUser.OpenProjectInstanceId, request.Alias, request.ClientId, cipher);
        await opInstanceService.Save(dto);
        return NoContent();
    }

    /// <summary>
    /// Público: la página de login la usa (sin sesión todavía) para saber qué instancias
    /// tienen OAuth conectado y ofrecer el botón "OAuth con OpenProject". Nunca expone
    /// clientId/clientSecret (ver ListsOpInstanceDto).
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> Lists()
    {
        var instances = await opInstanceService.Lists();
        var oauthInstances = instances.Where(x => x.IsOAuthConnected).ToList();
        return Ok(oauthInstances);
    }
}