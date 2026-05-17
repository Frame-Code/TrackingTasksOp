using System.Net.Http.Headers;
using System.Text;
using Application.Ports.Auth;
using Infrastructure.DataAccess;
using Infrastructure.DataAccess.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.Adapters.Http;

public class OpenProjectAuthHandler(IHttpContextAccessor httpContextAccessor) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var services = httpContextAccessor.HttpContext!.RequestServices;
        var currentUser = services.GetRequiredService<CurrentUser>();
        var context = services.GetRequiredService<TrackingTasksDbContext>();
        var encryptor = services.GetRequiredService<IApiKeyEncryptorService>();

        var instanceUrl = currentUser.OpenProjectInstanceUrl
            ?? throw new InvalidOperationException("El usuario autenticado no tiene una instancia de OpenProject asociada.");

        // Reescribir la URI con la URL base del usuario actual
        var relativePath = request.RequestUri?.PathAndQuery ?? "/";
        request.RequestUri = new Uri($"{instanceUrl.TrimEnd('/')}{relativePath}");

        // Obtener y desencriptar la API key del usuario
        var credential = await context.Set<LocalCredential>()
            .FirstOrDefaultAsync(x => x.UserId == currentUser.UserId, ct)
            ?? throw new InvalidOperationException($"No se encontró credencial local para el usuario {currentUser.UserId}.");

        var apiKey = encryptor.UnProtect(credential.EncryptedApiKey);
        var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes($"apikey:{apiKey}"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", encoded);

        return await base.SendAsync(request, ct);
    }
}
