using System.ComponentModel.DataAnnotations;
using Application.Dto.Auth;
using Application.Ports.Auth;
using Application.Ports.UseCases.Settings;
using Infrastructure.DataAccess.Entities;
using Infrastructure.DataAccess.Entities.Enums;
using Microsoft.AspNetCore.Identity;

namespace Infrastructure.Adapters.UseCases.Settings;

public class UpdateTaskPreferencesCommandImpl(
    UserManager<ApplicationUser> userManager,
    CurrentUser currentUser) : IUpdateTaskPreferencesCommand
{
    public async Task Execute(UpdateTaskPreferencesRequest request, CancellationToken ct = default)
    {
        if (!Enum.TryParse<PauseDefaultBehavior>(request.PauseDefaultBehavior, out var pauseBehavior))
            throw new ValidationException($"Comportamiento de pausa inválido: '{request.PauseDefaultBehavior}'.");

        var userId = currentUser.UserId
            ?? throw new UnauthorizedAccessException("Usuario no autenticado.");

        var appUser = await userManager.FindByIdAsync(userId)
            ?? throw new ApplicationException($"User {userId} not found while updating task preferences");

        appUser.PauseDefaultBehavior = pauseBehavior;
        appUser.SkipCancelConfirmation = request.SkipCancelConfirmation;
        appUser.AddRandomSlackTime = request.AddRandomSlackTime;

        var result = await userManager.UpdateAsync(appUser);
        if (!result.Succeeded)
            throw new ApplicationException(string.Join("; ", result.Errors.Select(e => e.Description)));
    }
}
