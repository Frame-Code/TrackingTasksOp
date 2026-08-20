using Application.Dto.Auth;
using Application.Ports.Auth;
using Application.Ports.UseCases.Settings;
using Infrastructure.DataAccess.Entities;
using Microsoft.AspNetCore.Identity;

namespace Infrastructure.Adapters.UseCases.Settings;

public class UpdateAiApiKeyCommandImpl(
    UserManager<ApplicationUser> userManager,
    IApiKeyEncryptorService encryptor,
    CurrentUser currentUser) : IUpdateAiApiKeyCommand
{
    public async Task Execute(UpdateAiApiKeyRequest request, CancellationToken ct = default)
    {
        var userId = currentUser.UserId
            ?? throw new UnauthorizedAccessException("Usuario no autenticado.");

        var appUser = await userManager.FindByIdAsync(userId)
            ?? throw new ApplicationException($"User {userId} not found while updating AI api key");

        // Vacío/null = quitar la key propia y volver a la compartida (con límite de uso).
        appUser.EncryptedGroqApiKey = string.IsNullOrWhiteSpace(request.ApiKey)
            ? null
            : encryptor.Protect(request.ApiKey);

        var result = await userManager.UpdateAsync(appUser);
        if (!result.Succeeded)
            throw new ApplicationException(string.Join("; ", result.Errors.Select(e => e.Description)));
    }
}
