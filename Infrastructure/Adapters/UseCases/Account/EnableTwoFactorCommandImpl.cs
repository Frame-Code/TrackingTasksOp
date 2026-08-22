using System.ComponentModel.DataAnnotations;
using Application.Dto.Auth;
using Application.Ports.Auth;
using Application.Ports.UseCases.Account;
using Infrastructure.DataAccess.Entities;
using Microsoft.AspNetCore.Identity;

namespace Infrastructure.Adapters.UseCases.Account;

public class EnableTwoFactorCommandImpl(
    UserManager<ApplicationUser> userManager,
    CurrentUser currentUser) : IEnableTwoFactorCommand
{
    /// <summary>Suficientes para varios reemplazos de teléfono sin volverse una lista impráctica de guardar.</summary>
    private const int RecoveryCodeCount = 8;

    public async Task<RecoveryCodesResponse> Execute(EnableTwoFactorRequest request, CancellationToken ct = default)
    {
        var userId = currentUser.UserId
            ?? throw new UnauthorizedAccessException("Usuario no autenticado.");

        var appUser = await userManager.FindByIdAsync(userId)
            ?? throw new ApplicationException($"User {userId} not found while enabling two factor");

        if (appUser.TwoFactorEnabled)
            throw new ValidationException("Ya tenés la verificación en dos pasos activada.");

        // La gente copia el código con espacios desde la app del teléfono.
        var code = TwoFactorCodes.Normalize(request.Code);

        var isValid = await userManager.VerifyTwoFactorTokenAsync(
            appUser, userManager.Options.Tokens.AuthenticatorTokenProvider, code);

        if (!isValid)
            throw new ValidationException(
                "El código no es válido. Revisá que la hora del teléfono esté sincronizada e intentá con el código actual.");

        await userManager.SetTwoFactorEnabledAsync(appUser, true);

        var codes = await userManager.GenerateNewTwoFactorRecoveryCodesAsync(appUser, RecoveryCodeCount);

        return new RecoveryCodesResponse(codes?.ToList() ?? []);
    }
}
