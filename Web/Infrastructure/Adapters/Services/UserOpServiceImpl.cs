using System.Net;
using System.Text.Json;
using Application.Ports.Services;
using Domain.Entities.OpenProjectEntities.User;
using Microsoft.Extensions.Options;
using Web.Infrastructure.Config.Extensions;
using Web.Infrastructure.Config.Settings;

namespace Web.Infrastructure.Adapters.Services;

public class UserOpServiceImpl(
    IHttpClientFactory httpClientFactory,
    ILogger<UserOpServiceImpl> logger,
    IOptions<OpenProjectSettings> settings
    ) : IUserOpService
{
    private readonly OpenProjectSettings _settings = settings.Value;
    private readonly HttpClient _client = httpClientFactory.CreateClient(settings.Value.HttpClientName);
    
    public async Task<List<User>> Lists()
    {
        logger.LogInformation("Executing Lists:UserOpServiceImpl");
        string url = $"{_settings.BaseUrl}/api/v3/users";
        HttpResponseMessage response = await _client.GetAsync(url);
        
        if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Unauthorized)
            return new List<User>();
        
        if (!response.IsSuccessStatusCode)
        {
            string error = await response.Content.ReadAsStringAsync();
            throw new Exception($"Error HTTP {(int)response.StatusCode}: {error}");
        }
        
        string json = await response.Content.ReadAsStringAsync();
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var collection = JsonSerializer.Deserialize<UserCollection>(json, options);

        return collection?.Embedded?.Elements ?? new List<User>();
    }

    public async Task<User?> FindByName(string name)
    {
        // En un entorno real, la API de OP permite buscar por filtros en el endpoint de users
        // pero por simplicidad listamos y buscamos en memoria o podemos usar el endpoint /api/v3/users?filters=...
        var users = await Lists();
        return users.FirstOrDefault(u => u.Name.Contains(name, StringComparison.OrdinalIgnoreCase));
    }
}
