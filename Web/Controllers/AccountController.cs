using Application.Dto.Auth;
using Application.Ports.UseCases.Account;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Web.Controllers;

/// <summary>
/// La cuenta del usuario autenticado: contraseña, segundo factor y avatar.
/// Va aparte de AuthController, que ya carga registro, login, logout y api-key.
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
public class AccountController(
    ISetupTwoFactorCommand setupTwoFactorCommand,
    IEnableTwoFactorCommand enableTwoFactorCommand,
    IRegenerateRecoveryCodesCommand regenerateRecoveryCodesCommand,
    IResetAuthenticatorCommand resetAuthenticatorCommand,
    IChangePasswordCommand changePasswordCommand,
    IUpdateAvatarCommand updateAvatarCommand,
    IGetAvatarQuery getAvatarQuery) : ControllerBase
{
    /// <summary>Devuelve el QR y la clave manual para enrolar la app. No activa nada todavía.</summary>
    [HttpPost("2fa/setup")]
    [EnableRateLimiting("auth")]
    public async Task<ActionResult<TwoFactorSetupResponse>> SetupTwoFactor(CancellationToken ct)
    {
        return await setupTwoFactorCommand.Execute(ct);
    }

    /// <summary>Confirma el código y activa el 2FA, devolviendo la primera tanda de códigos de recuperación.</summary>
    [HttpPost("2fa/enable")]
    [EnableRateLimiting("auth")]
    public async Task<ActionResult<RecoveryCodesResponse>> EnableTwoFactor(
        [FromBody] EnableTwoFactorRequest request, CancellationToken ct)
    {
        return await enableTwoFactorCommand.Execute(request, ct);
    }

    /// <summary>
    /// Emite códigos de recuperación nuevos e invalida los anteriores. Sirve para cuando el
    /// usuario no sabe dónde quedaron los suyos: no hay nada que "perder para siempre".
    /// </summary>
    [HttpPost("2fa/recovery-codes")]
    [EnableRateLimiting("auth")]
    public async Task<ActionResult<RecoveryCodesResponse>> RegenerateRecoveryCodes(
        [FromBody] EnableTwoFactorRequest request, CancellationToken ct)
    {
        return await regenerateRecoveryCodesCommand.Execute(request, ct);
    }

    /// <summary>
    /// Desvincula la app de autenticación para poder enrolar otro teléfono. Acepta un código de
    /// recuperación, que es la salida cuando el dispositivo original ya no está.
    /// </summary>
    [HttpPost("2fa/reset")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> ResetAuthenticator(
        [FromBody] ResetAuthenticatorRequest request, CancellationToken ct)
    {
        await resetAuthenticatorCommand.Execute(request, ct);
        return NoContent();
    }

    /// <summary>
    /// Cambia la contraseña. El límite "auth" (por IP) es lo único que frena la fuerza bruta
    /// sobre el código de 6 dígitos: Identity no bloquea la cuenta por códigos 2FA fallidos.
    /// </summary>
    [HttpPut("password")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request, CancellationToken ct)
    {
        await changePasswordCommand.Execute(request, ct);
        return NoContent();
    }

    [HttpPut("avatar")]
    public async Task<IActionResult> UpdateAvatar([FromBody] UpdateAvatarRequest request, CancellationToken ct)
    {
        await updateAvatarCommand.Execute(request, ct);
        return NoContent();
    }

    [HttpDelete("avatar")]
    public async Task<IActionResult> DeleteAvatar(CancellationToken ct)
    {
        await updateAvatarCommand.Execute(new UpdateAvatarRequest(null), ct);
        return NoContent();
    }

    /// <summary>
    /// Sirve el avatar para el &lt;img&gt; del sidebar. Lleva Last-Modified porque se pide en cada
    /// carga de página: con eso el navegador recibe un 304 en vez de rebajar la imagen entera.
    /// </summary>
    [HttpGet("avatar")]
    public async Task<IActionResult> GetAvatar(CancellationToken ct)
    {
        var avatar = await getAvatarQuery.Execute(ct);
        if (avatar is null) return NotFound();

        Response.Headers.LastModified = avatar.UpdatedAt.ToString("R");
        return File(avatar.Jpeg, "image/jpeg");
    }
}
