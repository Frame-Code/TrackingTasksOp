using System.ComponentModel.DataAnnotations;
using Application.Dto.Auth;
using Application.Ports.Auth;
using Application.Ports.UseCases.Account;
using Infrastructure.DataAccess.Entities;
using Microsoft.AspNetCore.Identity;

namespace Infrastructure.Adapters.UseCases.Account;

public class ChangePasswordCommandImpl(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    CurrentUser currentUser) : IChangePasswordCommand
{
    public async Task Execute(ChangePasswordRequest request, CancellationToken ct = default)
    {
        var userId = currentUser.UserId
            ?? throw new UnauthorizedAccessException("Usuario no autenticado.");

        var appUser = await userManager.FindByIdAsync(userId)
            ?? throw new ApplicationException($"User {userId} not found while changing password");

        // El segundo factor es obligatorio para esta operación. Si el usuario todavía no lo
        // activó, la UI tiene que mandarlo por el enrolamiento antes de mostrarle el formulario.
        if (!appUser.TwoFactorEnabled)
            throw new ValidationException(
                "Activá la verificación en dos pasos antes de cambiar tu contraseña.");

        await TwoFactorCodes.VerifyOrThrowAsync(userManager, appUser, request.TwoFactorCode);

        var result = await userManager.ChangePasswordAsync(
            appUser, request.CurrentPassword, request.NewPassword);

        if (!result.Succeeded)
            throw new ValidationException(string.Join("; ", result.Errors.Select(e => e.Description)));

        // ChangePasswordAsync rota el SecurityStamp, lo que invalida las cookies emitidas antes
        // — incluida la de esta misma sesión. Sin este refresh, cambiar la contraseña te expulsa
        // al login en el acto.
        await signInManager.RefreshSignInAsync(appUser);
    }
}
