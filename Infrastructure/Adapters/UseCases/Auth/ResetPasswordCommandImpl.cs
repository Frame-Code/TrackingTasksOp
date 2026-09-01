using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;
using Application.Dto.Auth;
using Application.Ports.UseCases.Auth;
using Infrastructure.DataAccess.Entities;
using Microsoft.AspNetCore.Identity;

namespace Infrastructure.Adapters.UseCases.Auth;

public class ResetPasswordCommandImpl(
    UserManager<ApplicationUser> userManager) : IResetPasswordCommand
{
    public async Task ExecuteAsync(ResetPasswordRequest request, CancellationToken ct = default)
    {
        var user = await userManager.FindByEmailAsync(request.Email);

        if (!IsCodeValid(user, request.Code))
            throw new ValidationException("El código es inválido o venció. Pedí uno nuevo.");

        var result = await userManager.RemovePasswordAsync(user!);
        if (result.Succeeded)
            result = await userManager.AddPasswordAsync(user!, request.NewPassword);

        if (!result.Succeeded)
            throw new ValidationException(string.Join("; ", result.Errors.Select(e => e.Description)));

        // Un código es de un solo uso: si no se limpia acá, alguien que lo interceptó podría
        // reutilizarlo hasta que venza.
        user!.PasswordResetCodeHash = null;
        user.PasswordResetCodeExpiresAt = null;
        await userManager.UpdateAsync(user);
    }

    private static bool IsCodeValid(ApplicationUser? user, string code)
    {
        if (user?.PasswordResetCodeHash is null || user.PasswordResetCodeExpiresAt is null)
            return false;

        if (user.PasswordResetCodeExpiresAt < DateTime.UtcNow)
            return false;

        var expected = Convert.FromHexString(user.PasswordResetCodeHash);
        var actual = SHA256.HashData(Encoding.UTF8.GetBytes(code));
        return CryptographicOperations.FixedTimeEquals(expected, actual);
    }
}
