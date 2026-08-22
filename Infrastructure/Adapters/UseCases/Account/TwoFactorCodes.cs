using System.ComponentModel.DataAnnotations;
using Infrastructure.DataAccess.Entities;
using Microsoft.AspNetCore.Identity;

namespace Infrastructure.Adapters.UseCases.Account;

internal static class TwoFactorCodes
{
    /// <summary>
    /// Las apps de autenticación muestran el código como "123 456" y la gente lo pega tal cual,
    /// espacio incluido. Identity compara el string exacto, así que sin esto un código correcto
    /// se rechaza y el usuario no entiende por qué.
    /// </summary>
    public static string Normalize(string? code) =>
        (code ?? string.Empty).Replace(" ", string.Empty).Replace("-", string.Empty).Trim();

    /// <summary>
    /// Acepta el código de la app o uno de recuperación, y tira si ninguno sirve. Vive acá
    /// porque lo necesitan las tres operaciones sensibles (cambiar contraseña, regenerar
    /// códigos y desvincular el dispositivo): tener tres copias de esta lógica es la forma
    /// de que una quede desactualizada.
    ///
    /// Ojo: canjear un código de recuperación lo consume.
    /// </summary>
    public static async Task VerifyOrThrowAsync(
        UserManager<ApplicationUser> userManager, ApplicationUser appUser, string? rawCode)
    {
        var isValid = await userManager.VerifyTwoFactorTokenAsync(
            appUser, userManager.Options.Tokens.AuthenticatorTokenProvider, Normalize(rawCode));

        if (isValid) return;

        var redeemed = await userManager.RedeemTwoFactorRecoveryCodeAsync(
            appUser, rawCode?.Trim() ?? string.Empty);

        if (redeemed.Succeeded) return;

        throw new ValidationException("El código de verificación es incorrecto.");
    }
}
