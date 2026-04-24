using System;
using System.Collections.Generic;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using Application.Ports.Services;
using Domain.Entities.OpenProjectEntities.Status;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Web.Infrastructure.Config.Settings;

namespace Web.Infrastructure.Adapters.Services;

public class StatusOpServiceImpl(
    IHttpClientFactory httpClientFactory,
    ILogger<StatusOpServiceImpl> logger,
    IOptions<OpenProjectSettings> settings
    ) : IStatusOpService
{
    private readonly OpenProjectSettings _settings = settings.Value;
    private readonly HttpClient _client = httpClientFactory.CreateClient(settings.Value.HttpClientName);
    
    public async Task<List<Status>> Lists()
    {
        logger.LogInformation("Executing Lists:StatusOpServiceImpl");
        string url = BuildUrl();
        HttpResponseMessage response = await _client.GetAsync(url);
        if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Unauthorized)
            return new List<Status>();
        
        if (!response.IsSuccessStatusCode)
        {
            string error = await response.Content.ReadAsStringAsync();
            throw new Exception($"Error HTTP {(int)response.StatusCode}: {error}");
        }
        
        string json = await response.Content.ReadAsStringAsync();
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var collection = JsonSerializer.Deserialize<StatusCollection>(json, options);

        return collection?.Embedded?.Elements 
               ?? new List<Status>();
    }

    public async Task<Status?> FindByNameAsync(string name)
    {
        var statuses = await Lists();
        if (!statuses.Any()) return null;

        var normalizedName = name.Trim();

        // 1. Búsqueda por coincidencia exacta (ignorando mayúsculas/minúsculas)
        var exactMatch = statuses.FirstOrDefault(s => 
            s.Name.Equals(normalizedName, StringComparison.OrdinalIgnoreCase));

        if (exactMatch != null)
        {
            logger.LogInformation("Found exact status match for '{Name}'. ID: {Id}", name, exactMatch.Id);
            return exactMatch;
        }

        // 2. Si no hay coincidencia exacta, búsqueda por contenido (más flexible)
        var containsMatch = statuses.FirstOrDefault(s => 
            s.Name.Contains(normalizedName, StringComparison.OrdinalIgnoreCase));
        
        if (containsMatch != null)
        {
            logger.LogInformation("Found partial status match for '{Name}'. Matched with '{MatchedName}'. ID: {Id}", name, containsMatch.Name, containsMatch.Id);
        }

        return containsMatch;
    }

    private string BuildUrl()
    {
        return $"{_settings.BaseUrl}/api/v3/statuses";
    }
}