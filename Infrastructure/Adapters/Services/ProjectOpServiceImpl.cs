using System.Net;
using System.Text.Json;
using Application.Ports.Services;
using Domain.Entities.OpenProjectEntities.Project;
using Infrastructure.Settings;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.Adapters.Services;

public class ProjectOpServiceImpl(
    IHttpClientFactory httpClientFactory,
    ILogger<StatusOpServiceImpl> logger,
    IOptions<OpenProjectSettings> iSettings
    ) : IProjectOpService
{
    private readonly OpenProjectSettings _settings = iSettings.Value;
    private readonly HttpClient _client = httpClientFactory.CreateClient(iSettings.Value.HttpClientName);
    
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
        return $"{_settings.BaseUrl}/api/v3/projects";
    }
}