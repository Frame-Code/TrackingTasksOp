using System.Net;
using System.Runtime.Serialization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Application.Ports.Services;
using Domain.Entities.OpenProjectEntities.Activity;
using Microsoft.Extensions.Options;
using Web.Infrastructure.Config.Settings;

namespace Web.Infrastructure.Adapters.Services;

public class ActivityOpServiceImpl(
    IHttpClientFactory httpClientFactory,
    ILogger<ActivityOpServiceImpl> logger,
    IOptions<OpenProjectSettings> iSettings
    ) : IActivityOpService
{
    private readonly OpenProjectSettings _settings = iSettings.Value;
    private readonly HttpClient _client = httpClientFactory.CreateClient(iSettings.Value.HttpClientName);
    
    public async Task<List<ActivityAllowedValue>> Lists(int idWorkPackage)
    {
        try 
        {
            logger.LogInformation("Executing function Lists from ActivityServiceImpl for WP {Id}", idWorkPackage);
            var url = BuildUrl();
            var payload = BuildPayload(idWorkPackage);
            var response = await _client.PostAsync(url, payload);
            
            if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                logger.LogWarning("Access denied or not found when listing activities for WP {Id}. Status: {Status}", idWorkPackage, response.StatusCode);
                return new List<ActivityAllowedValue>();
            }
            
            var jsonResponse = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Error calling time_entries/form: {Response}", jsonResponse);
                return new List<ActivityAllowedValue>();
            }
            
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var collection = JsonSerializer.Deserialize<ActivityCollection>(jsonResponse, options)
                ?? throw new SerializationException("Can't deserialize response to ActivityCollection");
            
            return collection.Embedded.Schema.Type.Embedded.AllowedValues;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error unexpected in ActivityOpService");
            return new List<ActivityAllowedValue>(); 
        }
    }
    
    public async Task<ActivityAllowedValue?> FindByNameAsync(string name, int workPackageId)
    {
        var activities = await Lists(workPackageId);
        if (!activities.Any()) return null;

        var normalizedName = name.Trim();

        // 1. Búsqueda por coincidencia exacta (ignorando mayúsculas/minúsculas)
        var exactMatch = activities.FirstOrDefault(a => 
            a.Name.Equals(normalizedName, StringComparison.OrdinalIgnoreCase));

        if (exactMatch != null)
        {
            logger.LogInformation("Found exact activity match for '{Name}' in WP {WpId}. ID: {Id}", name, workPackageId, exactMatch.Id);
            return exactMatch;
        }

        // 2. Si no hay coincidencia exacta, búsqueda por contenido (más flexible)
        var containsMatch = activities.FirstOrDefault(a => 
            a.Name.Contains(normalizedName, StringComparison.OrdinalIgnoreCase));
    
        if (containsMatch != null)
        {
            logger.LogInformation("Found partial activity match for '{Name}' in WP {WpId}. Matched with '{MatchedName}'. ID: {Id}", name, workPackageId, containsMatch.Name, containsMatch.Id);
        }

        return containsMatch;
    }

    private string BuildUrl()
    {
        return $"{_settings.BaseUrl}/api/v3/time_entries/form";
    }

    private StringContent BuildPayload(int idWorkPackage)
    {
        var payload = new JsonObject
        {
            ["_links"] = new JsonObject
            {
                ["workPackage"] = new JsonObject
                {
                    ["href"] = $"/api/v3/work_packages/{idWorkPackage}"
                }
            }
        };
        
        return new StringContent(payload.ToJsonString(), Encoding.UTF8, "application/json");
    }
}
