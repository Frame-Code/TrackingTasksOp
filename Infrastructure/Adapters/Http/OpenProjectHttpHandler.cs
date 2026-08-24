using Application.Ports.Auth;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.Adapters.Http;

public class OpenProjectAuthHandler(IHttpContextAccessor httpContextAccessor) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var services = httpContextAccessor.HttpContext!.RequestServices;
        var currentUser = services.GetRequiredService<CurrentUser>();

        // La instancia sale de los claims, sin tocar la BD.
        var instanceUrl = currentUser.OpenProjectInstanceUrl
            ?? throw new InvalidOperationException("El usuario autenticado no tiene una instancia de OpenProject asociada.");

        // Reescribir la URI con la URL base del usuario actual
        var relativePath = request.RequestUri?.PathAndQuery ?? "/";
        request.RequestUri = new Uri($"{instanceUrl.TrimEnd('/')}{relativePath}");

        // La credencial se resuelve una sola vez por request: si el request dispara
        // varias llamadas en paralelo, consultar la BD aquí rompía el DbContext scoped
        // ("A second operation was started on this context instance..."). Basic (API key) o
        // Bearer (OAuth) según qué credencial tenga el usuario — ver OpenProjectAuthHeaderProvider.
        var headerProvider = services.GetRequiredService<OpenProjectAuthHeaderProvider>();
        request.Headers.Authorization = await headerProvider.GetAuthorizationHeaderAsync();

        return await base.SendAsync(request, ct);
    }
}
