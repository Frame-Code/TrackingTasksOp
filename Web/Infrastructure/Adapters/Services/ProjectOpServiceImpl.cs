using System.Net;
using System.Text.Json;
using Application.Ports.Services;
using Domain.Entities.OpenProjectEntities;
using Web.Infrastructure.Config.Extensions;
using Web.Infrastructure.Config.Settings;

namespace Web.Infrastructure.Adapters.Services;

public class ProjectOpServiceImpl(
    IHttpClientFactory httpClientFactory,
    ILogger<StatusOpServiceImpl> logger,
    [FromKeyedServices(nameof(KeyService.OpenProjectSettings))]
    IApiSettings settings
    ) : IProjectOpService
{
    private readonly HttpClient _client = httpClientFactory.CreateClient(settings.GetHttpClientName());
    
    public async Task<List<Project>> Lists()
    {
        logger.LogInformation("Executing function Lists from ProjectOpService");
        string url = BuildUrl();
        HttpResponseMessage response = await _client.GetAsync(url);
        if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Unauthorized)
            return new List<Project>();
        
        if (!response.IsSuccessStatusCode)
        {
            string error = await response.Content.ReadAsStringAsync();
            throw new Exception($"Error HTTP {(int)response.StatusCode}: {error}");
        }
        
        string json = await response.Content.ReadAsStringAsync();
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var collection = JsonSerializer.Deserialize<ProjectCollection>(json, options);

        return collection?.Embedded?.Projects
               ?? new List<Project>();
    }
    
    private string BuildUrl()
    {
        return $"{settings.GetUri()}/api/v3/projects";
    }
}