using Application.Dto.Auth;
using Application.Ports.Auth;
using Application.Ports.Services;
using Application.Ports.UseCases.Auth;
using Infrastructure.DataAccess.Entities;
using Infrastructure.Settings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Web.Controllers.Dto.HttpRequest;

namespace Web.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class AuthController(
    IRegisterLocalUserCommand registerLocalUserCommand,
    ILoginLocalUserCommand loginLocalUserCommand,
    IUpdateApiKeyCommand updateApiKeyCommand,
    IInitializerInstanceService initializerInstanceService,
    IOAuthService oAuthService,
    IOAuthLoginCommand oAuthLoginCommand,
    IRevokeOAuthSessionCommand revokeOAuthSessionCommand,
    IForgotPasswordCommand forgotPasswordCommand,
    IResetPasswordCommand resetPasswordCommand,
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    CurrentUser currentUser,
    [FromKeyedServices(KeyedServicesNames.OpenProjectUrlService)]
    BaseUrlService urlService) : ControllerBase
{
    
    [HttpPost("local-register")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
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

        var principal = await signInManager.CreateUserPrincipalAsync(appUser);
        await HttpContext.SignInAsync(IdentityConstants.ApplicationScheme, principal);
        var initializeRequest = new InitializeInstanceRequest
        {
            OpenProjectInstanceId = appUser.OpenProjectInstanceId,
            UserId = appUser.Id,
            Username = appUser.UserName ?? "-",
            OpenProjectInstanceUrl = opUrlNormalized,
            ApiKey = request.ApiKey
        };
        await initializerInstanceService.InitializeAsync(initializeRequest, ct);
        return Ok(new
        {
            userId = response.Data!.UserId,
            email = response.Data.Email,
            openProjectInstanceUrl = response.Data!.OpenProjectInstanceUrl
        });
    }
    
    [HttpPost("local-login")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> LocalLoginAsync(LocalLoginHttpRequest request, CancellationToken ct)
    {
        var commandRequest = new LocalLoginRequest { Email = request.Email, Password = request.Password };
        var response = await loginLocalUserCommand.ExecuteAsync(commandRequest, ct);

        if (!response.IsSuccess)
            return Unauthorized(new { message = response.ErrorMessage });

        var appUser = await userManager.FindByEmailAsync(response.Data!.Email)
            ?? throw new ApplicationException($"User with email {response.Data.Email} not found after login");

        var principal = await signInManager.CreateUserPrincipalAsync(appUser);
        await HttpContext.SignInAsync(IdentityConstants.ApplicationScheme, principal);

        return Ok(response.Data);
    }
    
    /// <summary>
    /// Actualiza la API key de OpenProject del usuario autenticado (ej. si la clave de
    /// Data Protection que la cifraba se perdió y quedó indescifrable). No requiere
    /// [AllowAnonymous]: el filtro de autorización global ya exige sesión iniciada.
    /// </summary>
    [HttpPut("api-key")]
    public async Task<IActionResult> UpdateApiKeyAsync(UpdateApiKeyHttpRequest request, CancellationToken ct)
    {
        var commandRequest = new UpdateApiKeyRequest { ApiKey = request.ApiKey };
        var response = await updateApiKeyCommand.ExecuteAsync(commandRequest, ct);
        if (!response.IsSuccess)
        {
            return BadRequest(new
            {
                message = response.ErrorMessage,
            });
        }

        return Ok(response.Data);
    }

    /// <summary>
    /// Manda un código de 6 dígitos por correo si ese email existe. Responde igual exista o no
    /// el usuario, para no filtrar qué correos están registrados.
    /// </summary>
    [HttpPost("forgot-password")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> ForgotPasswordAsync(ForgotPasswordHttpRequest request, CancellationToken ct)
    {
        await forgotPasswordCommand.ExecuteAsync(new ForgotPasswordRequest(request.Email), ct);
        return Ok(new { message = "Si el correo existe, vas a recibir un código de recuperación." });
    }

    [HttpPost("reset-password")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> ResetPasswordAsync(ResetPasswordHttpRequest request, CancellationToken ct)
    {
        await resetPasswordCommand.ExecuteAsync(
            new ResetPasswordRequest(request.Email, request.Code, request.NewPassword), ct);
        return NoContent();
    }

    [HttpPost("logout")]
    public async Task<IActionResult> LogoutAsync(CancellationToken ct)
    {
        await revokeOAuthSessionCommand.Execute(ct);
        await HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
        return NoContent();
    }

    [HttpGet("oauth/authorize")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Authorize(int instanceId)
    {
        var state = await oAuthService.GenerateOAuthState(instanceId);
        var urlAuthorize = await oAuthService.GenerateAuthorizeUrl(state, instanceId);
        return Redirect(urlAuthorize);
    }

    [HttpGet("oauth/callback")]
    [AllowAnonymous]
    public async Task<IActionResult> OAuthCallback(string code, string state, CancellationToken ct)
    {
        var response = await oAuthLoginCommand.ExecuteAsync(code, state, ct);
        if (!response.IsSuccess)
            return BadRequest(new { message = response.ErrorMessage });

        var appUser = await userManager.FindByIdAsync(response.Data!.UserId)
            ?? throw new ApplicationException($"User {response.Data.UserId} not found after OAuth login");

        var principal = await signInManager.CreateUserPrincipalAsync(appUser);
        await HttpContext.SignInAsync(IdentityConstants.ApplicationScheme, principal);

        // Este endpoint lo pega el navegador con una redirección completa (es el redirect_uri
        // de OpenProject), no un fetch del frontend: no tiene sentido devolver JSON acá, nadie
        // lo lee por JS. Se manda a una página puente que llama a GET /auth/me (con la cookie
        // recién emitida) para poblar sessionStorage.currentUser antes de entrar a la app.
        return Redirect("/oauth-callback.html");
    }

    /// <summary>
    /// "Quién soy": lo usa la página puente del callback OAuth para poblar sessionStorage.currentUser
    /// con la cookie recién emitida por SignInAsync (no hay otra forma de obtener esos datos ahí,
    /// porque el callback es una navegación de página completa, no un fetch del frontend).
    /// </summary>
    [HttpGet("me")]
    public async Task<IActionResult> Me()
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedAccessException("Usuario no autenticado.");
        var appUser = await userManager.FindByIdAsync(userId)
            ?? throw new ApplicationException($"User {userId} not found");

        return Ok(new AuthenticatedUserResponse
        {
            UserId = appUser.Id,
            Email = appUser.Email!,
            Name = appUser.UserName!,
            OpenProjectInstanceUrl = appUser.OpenProjectInstanceBaseUrl,
            OpenProjectInstanceId = appUser.OpenProjectInstanceId
        });
    }
}