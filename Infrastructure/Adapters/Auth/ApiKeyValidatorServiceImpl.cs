using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Application.Ports.Auth;
using Domain.Entities.OpenProjectEntities.User;
using Infrastructure.Exceptions;

namespace Infrastructure.Adapters.Auth;

public class ApiKeyValidatorServiceImpl(IHttpClientFactory clientFactory) : IApiKeyValidatorService
{
    public async Task<User> ValidateAsync(string instanceUrl, string apiKey, CancellationToken ct)
    {
        var client = clientFactory.CreateClient();
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