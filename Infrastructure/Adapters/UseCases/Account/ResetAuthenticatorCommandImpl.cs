using System.ComponentModel.DataAnnotations;
using Application.Dto.Auth;
using Application.Ports.Auth;
using Application.Ports.UseCases.Account;
using Infrastructure.DataAccess.Entities;
using Microsoft.AspNetCore.Identity;

namespace Infrastructure.Adapters.UseCases.Account;

public class ResetAuthenticatorCommandImpl(
    UserManager<ApplicationUser> userManager,
    CurrentUser currentUser) : IResetAuthenticatorCommand
{
    public async Task Execute(ResetAuthenticatorRequest request, CancellationToken ct = default)
    {
        var userId = currentUser.UserId
            ?? throw new UnauthorizedAccessException("Usuario no autenticado.");

        var appUser = await userManager.FindByIdAsync(userId)
            ?? throw new ApplicationException($"User {userId} not found while resetting authenticator");

        if (!appUser.TwoFactorEnabled)
            throw new ValidationException("No tenés la verificación en dos pasos activada.");

        // Contraseña además del segundo factor: desvincular el dispositivo baja el nivel de
        // protección de la cuenta, así que no puede quedar al alcance de una sesión robada.
        if (!await userManager.CheckPasswordAsync(appUser, request.CurrentPassword))
            throw new ValidationException("La contraseña actual es incorrecta.");

        // Acepta un código de recuperación, que es el punto: si el teléfono ya no está, ese es
        // el único factor que al usuario le queda.
        await TwoFactorCodes.VerifyOrThrowAsync(userManager, appUser, request.TwoFactorCode);

        await userManager.SetTwoFactorEnabledAsync(appUser, false);

        // Rota la clave: la app vieja queda inservible aunque siga instalada en el teléfono
        // perdido. Sin esto, quien lo tenga sigue generando códigos válidos.
        await userManager.ResetAuthenticatorKeyAsync(appUser);
    }
}
