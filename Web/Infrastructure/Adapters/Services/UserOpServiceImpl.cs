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
        logger.LogInformation("Executing function Lists from UserOpService");
        string url = BuildUrl();

        try 
        {
            HttpResponseMessage response = await _client.GetAsync(url);

            if (response.StatusCode == HttpStatusCode.Forbidden)
            {
                logger.LogWarning("Access denied (403) to global user list. Falling back to 'me' user.");
                var me = await GetMe();
                return me != null ? new List<User> { me } : new List<User>();
            }

            if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Unauthorized)
            {
                logger.LogWarning("Unauthorized or not found when listing users. Status: {Status}", response.StatusCode);
                return new List<User>();
            }

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
        catch (Exception ex) when (ex is not UnauthorizedAccessException)
        {
            logger.LogError(ex, "Error fetching users. Attempting fallback to 'me'.");
            var me = await GetMe();
            return me != null ? new List<User> { me } : new List<User>();
        }
    }

    private async Task<User?> GetMe()
    {
        try
        {
            string url = $"{_settings.BaseUrl.TrimEnd('/')}/api/v3/users/me";
            var response = await _client.GetAsync(url);
            if (!response.IsSuccessStatusCode) return null;

            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<User>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch { return null; }
    }
    private string BuildUrl()
    {
        return $"{_settings.BaseUrl.TrimEnd('/')}/api/v3/users";
    }

    public async Task<User?> FindByName(string name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        
        // El usuario puede pedir asignarse algo a sí mismo usando "me"
        if (name.Equals("me", StringComparison.OrdinalIgnoreCase) || name.Equals("yo", StringComparison.OrdinalIgnoreCase) || name.Equals("mí", StringComparison.OrdinalIgnoreCase))
        {
            return await GetMe();
        }

        var users = await Lists();
        return users.FirstOrDefault(u => u.Name.Contains(name, StringComparison.OrdinalIgnoreCase));
    }
}
