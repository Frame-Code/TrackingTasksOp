using System.Net.Http.Headers;
using Application.Ports.Auth;
using Infrastructure.DataAccess.Entities;
using Infrastructure.Settings;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Infrastructure.Adapters.Services.Bot;

/// <summary>
/// Resuelve qué Bearer usar contra la API de Groq: la key propia del usuario si la
/// configuró (BYOK, sin límite de uso), o la compartida del servidor como fallback.
/// </summary>
public class GroqAuthHeaderProvider(
    UserManager<ApplicationUser> userManager,
    CurrentUser currentUser,
    IApiKeyEncryptorService encryptor,
    IOptions<GroqSettings> groqSettings)
{
    public async Task<AuthenticationHeaderValue> GetAuthorizationHeaderAsync()
    {
        var userId = currentUser.UserId;
        if (userId is not null)
        {
            var appUser = await userManager.FindByIdAsync(userId);
            if (!string.IsNullOrEmpty(appUser?.EncryptedGroqApiKey))
                return new AuthenticationHeaderValue("Bearer", encryptor.UnProtect(appUser.EncryptedGroqApiKey));
        }

        return new AuthenticationHeaderValue("Bearer", groqSettings.Value.ApiKey);
    }
}
