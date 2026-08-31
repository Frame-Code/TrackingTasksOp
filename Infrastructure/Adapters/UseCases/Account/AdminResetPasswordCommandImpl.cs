using System.ComponentModel.DataAnnotations;
using Application.Dto.Auth;
using Application.Ports.Auth;
using Application.Ports.UseCases.Account;
using Infrastructure.DataAccess.Entities;
using Microsoft.AspNetCore.Identity;

namespace Infrastructure.Adapters.UseCases.Account;

public class AdminResetPasswordCommandImpl(
    UserManager<ApplicationUser> userManager,
    CurrentUser currentUser) : IAdminResetPasswordCommand
{
    public async Task Execute(AdminResetPasswordRequest request, CancellationToken ct = default)
    {
        var adminId = currentUser.UserId
            ?? throw new UnauthorizedAccessException("Usuario no autenticado.");

        var admin = await userManager.FindByIdAsync(adminId)
            ?? throw new ApplicationException($"User {adminId} not found while resetting a password");

        if (!admin.IsAppAdmin)
            throw new UnauthorizedAccessException("Solo un admin de la app puede resetear la contraseña de otro usuario.");

        var target = await userManager.FindByEmailAsync(request.Email);

        // Mismo mensaje exista o no el usuario, y también si es de otra instancia: no filtrar
        // qué correos están registrados en instancias ajenas.
        if (target is null || target.OpenProjectInstanceId != admin.OpenProjectInstanceId)
            throw new ValidationException("No hay ningún usuario con ese correo en tu instancia.");

        await userManager.RemovePasswordAsync(target);
        var result = await userManager.AddPasswordAsync(target, request.NewPassword);

        if (!result.Succeeded)
            throw new ValidationException(string.Join("; ", result.Errors.Select(e => e.Description)));
    }
}
