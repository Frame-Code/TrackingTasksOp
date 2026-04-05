using System.Net;
using System.Runtime.Serialization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Application.Ports.Services;
using Domain.Entities.OpenProjectEntities.Activity;
using Web.Infrastructure.Config.Extensions;
using Web.Infrastructure.Config.Settings;

namespace Web.Infrastructure.Adapters.Services;

public class ActivityOpServiceImpl(
    IHttpClientFactory httpClientFactory,
    ILogger<ActivityOpServiceImpl> logger,
    [FromKeyedServices(nameof(KeyService.OpenProjectSettings))]
    IApiSettings settings
    ) : IActivityOpService
{
    private readonly HttpClient _client = httpClientFactory.CreateClient(settings.GetHttpClientName());
    
    public async Task<List<ActivityAllowedValue>> Lists(int idWorkPackage)
    {
<<<<<<< HEAD
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
=======
        logger.LogInformation("Executing function Lists from ActivityServiceImpl");
        var url = BuildUrl();
        var payload = BuildPayload(idWorkPackage);
        var response = await _client.PostAsync(url, payload);
        
        if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Unauthorized)
            return new List<ActivityAllowedValue>();
        
        var jsonResponse = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
            throw new Exception($"Error HTTP {(int)response.StatusCode}: {jsonResponse}");
        
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var collection = JsonSerializer.Deserialize<ActivityCollection>(jsonResponse, options)
            ?? throw new SerializationException("Can't deserialize response to ActivityCollection");
        
        return collection.Embedded.Schema.Type.Embedded.AllowedValues;
>>>>>>> 43a2f6a7702ea35cec6982dd843371d7f6bf2b9b
    }
    
    private string BuildUrl()
    {
        return $"{settings.GetUri()}/api/v3/time_entries/form";
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
