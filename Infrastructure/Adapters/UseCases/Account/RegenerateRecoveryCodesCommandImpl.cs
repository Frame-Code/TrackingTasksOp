using System.ComponentModel.DataAnnotations;
using Application.Dto.Auth;
using Application.Ports.Auth;
using Application.Ports.UseCases.Account;
using Infrastructure.DataAccess.Entities;
using Microsoft.AspNetCore.Identity;

namespace Infrastructure.Adapters.UseCases.Account;

public class RegenerateRecoveryCodesCommandImpl(
    UserManager<ApplicationUser> userManager,
    CurrentUser currentUser) : IRegenerateRecoveryCodesCommand
{
    private const int RecoveryCodeCount = 8;

    public async Task<RecoveryCodesResponse> Execute(EnableTwoFactorRequest request, CancellationToken ct = default)
    {
        var userId = currentUser.UserId
            ?? throw new UnauthorizedAccessException("Usuario no autenticado.");

        var appUser = await userManager.FindByIdAsync(userId)
            ?? throw new ApplicationException($"User {userId} not found while regenerating recovery codes");

        if (!appUser.TwoFactorEnabled)
            throw new ValidationException("No tenés la verificación en dos pasos activada.");

        // Pide el segundo factor: si alcanzara con la sesión, cualquiera con el navegador
        // abierto podría emitirse códigos y quedarse con acceso permanente.
        await TwoFactorCodes.VerifyOrThrowAsync(userManager, appUser, request.Code);

        var codes = await userManager.GenerateNewTwoFactorRecoveryCodesAsync(appUser, RecoveryCodeCount);

        return new RecoveryCodesResponse(codes?.ToList() ?? []);
    }
}
