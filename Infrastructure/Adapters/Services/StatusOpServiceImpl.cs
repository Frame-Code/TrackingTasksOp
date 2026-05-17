using System.Net;
using System.Text.Json;
using Application.Ports.Services;
using Domain.Entities.OpenProjectEntities.Status;
using Infrastructure.Settings;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.Adapters.Services;

public class StatusOpServiceImpl(
    IHttpClientFactory httpClientFactory,
    ILogger<StatusOpServiceImpl> logger
    ) : IStatusOpService
{
    private readonly HttpClient _client = httpClientFactory.CreateClient(KeyedServicesNames.OpenProjectHttpClientName);
    
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

    private string BuildUrl()
    {
        return $"/api/v3/statuses";
    }
}