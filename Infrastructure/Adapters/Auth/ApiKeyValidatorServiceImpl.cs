using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Application.Ports.Auth;
using Infrastructure.Exceptions;
using Infrastructure.Settings;
using User = Domain.Entities.OpenProjectEntities.User.User;

namespace Infrastructure.Adapters.Auth;

public class ApiKeyValidatorServiceImpl(IHttpClientFactory clientFactory) : IApiKeyValidatorService
{
    public async Task<User> ValidateAsync(string instanceUrl, string apiKey, CancellationToken ct)
    {
        // Cliente con protección SSRF: instanceUrl la declara el usuario (al registrarse o
        // al actualizar su API key), así que no puede tratarse como un destino confiable.
        var client = clientFactory.CreateClient(KeyedServicesNames.OpenProjectValidationHttpClientName);
        client.BaseAddress = new Uri(instanceUrl.TrimEnd('/'));
        client.Timeout = TimeSpan.FromSeconds(20);

        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"apikey:{apiKey}"));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        
        try
        {
            var response = await client.GetAsync("/api/v3/users/me", ct);
            
            if (response.StatusCode == HttpStatusCode.Unauthorized)
                throw new InvalidApiKeyException("API key inválida o sin permisos");
            
            if (!response.IsSuccessStatusCode)
                throw new OpenProjectRequestException($"OpenProject respondió {(int)response.StatusCode}");
            
            var body = await response.Content.ReadAsStringAsync(ct);
            var user = JsonSerializer.Deserialize<User>(body)
                ?? throw new InvalidOperationException("No se pudo deserializar la respuesta de /api/v3/users/me");

            return user;
        }
        catch (HttpRequestException)
        {
            throw new OpenProjectRequestException($"No se pudo conectar a OpenProject en {instanceUrl}");
        }
        catch (TaskCanceledException)
        {
            throw new OpenProjectRequestException($"Timeout al conectar a OpenProject en {instanceUrl}");
        }
    }
}