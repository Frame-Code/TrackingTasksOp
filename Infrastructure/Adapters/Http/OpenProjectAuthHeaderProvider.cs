using System.Text;
using Application.Ports.Auth;
using Infrastructure.DataAccess;
using Infrastructure.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Adapters.Http;

/// <summary>
/// Resuelve UNA sola vez por request el header Basic de OpenProject.
///
/// El <see cref="TrackingTasksDbContext"/> es scoped y no admite operaciones
/// concurrentes: si un request dispara varias llamadas HTTP en paralelo y cada una
/// consulta la credencial por su cuenta, EF Core lanza "A second operation was
/// started on this context instance before a previous operation completed".
/// Aquí la consulta se hace una vez y todas las llamadas esperan el mismo Task.
///
/// Tiene que ser un servicio SCOPED aparte, no estado dentro del DelegatingHandler:
/// IHttpClientFactory reutiliza los handlers entre requests (y entre usuarios), así
/// que cachear la credencial ahí filtraría la API key de un usuario a otro.
/// </summary>
public class OpenProjectAuthHeaderProvider(
    TrackingTasksDbContext context,
    CurrentUser currentUser,
    IApiKeyEncryptorService encryptor)
{
    private readonly object _gate = new();
    private Task<string>? _header;

    public Task<string> GetBasicHeaderAsync()
    {
        if (_header is not null) return _header;

        lock (_gate)
        {
            _header ??= LoadAsync();
        }

        return _header;
    }

    private async Task<string> LoadAsync()
    {
        // Sin CancellationToken a propósito: el Task se comparte entre todas las
        // llamadas del request, así que cancelar una no debe tumbar a las demás.
        var credential = await context.Set<LocalCredential>()
            .FirstOrDefaultAsync(x => x.UserId == currentUser.UserId)
            ?? throw new InvalidOperationException($"No se encontró credencial local para el usuario {currentUser.UserId}.");

        var apiKey = encryptor.UnProtect(credential.EncryptedApiKey);
        return Convert.ToBase64String(Encoding.UTF8.GetBytes($"apikey:{apiKey}"));
    }
}
