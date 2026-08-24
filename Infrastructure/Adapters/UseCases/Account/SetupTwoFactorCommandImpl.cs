using System.ComponentModel.DataAnnotations;
using System.Text;
using Application.Dto.Auth;
using Application.Ports.Auth;
using Application.Ports.Services;
using Application.Ports.UseCases.Account;
using Infrastructure.DataAccess.Entities;
using Microsoft.AspNetCore.Identity;

namespace Infrastructure.Adapters.UseCases.Account;

public class SetupTwoFactorCommandImpl(
    UserManager<ApplicationUser> userManager,
    CurrentUser currentUser,
    IQrCodeService qrCodeService) : ISetupTwoFactorCommand
{
    /// <summary>Nombre que muestra la app de autenticación junto al código.</summary>
    private const string Issuer = "TrackingTaskOp";

    public async Task<TwoFactorSetupResponse> Execute(CancellationToken ct = default)
    {
        var userId = currentUser.UserId
            ?? throw new UnauthorizedAccessException("Usuario no autenticado.");

        var appUser = await userManager.FindByIdAsync(userId)
            ?? throw new ApplicationException($"User {userId} not found while setting up two factor");

        // Si ya lo tiene activo, re-enrolar rotaría la clave y dejaría inservibles los códigos
        // de recuperación que el usuario guardó. Cambiar de teléfono es otra feature, con su
        // propia verificación; acá se corta.
        if (appUser.TwoFactorEnabled)
            throw new ValidationException("Ya tenés la verificación en dos pasos activada.");

        var key = await userManager.GetAuthenticatorKeyAsync(appUser);
        if (string.IsNullOrEmpty(key))
        {
            await userManager.ResetAuthenticatorKeyAsync(appUser);
            key = await userManager.GetAuthenticatorKeyAsync(appUser)
                ?? throw new ApplicationException("No se pudo generar la clave del authenticator");
        }

        var otpAuthUri =
            $"otpauth://totp/{Uri.EscapeDataString(Issuer)}:{Uri.EscapeDataString(appUser.Email!)}" +
            $"?secret={key}&issuer={Uri.EscapeDataString(Issuer)}&digits=6";

        return new TwoFactorSetupResponse(qrCodeService.ToPngDataUri(otpAuthUri), FormatKey(key));
    }

    /// <summary>Agrupa la clave de a 4 caracteres: si el QR falla, hay que tipearla a mano.</summary>
    private static string FormatKey(string key)
    {
        var result = new StringBuilder();
        for (var i = 0; i < key.Length; i += 4)
            result.Append(key.AsSpan(i, Math.Min(4, key.Length - i))).Append(' ');

        return result.ToString().Trim().ToLowerInvariant();
    }
}
