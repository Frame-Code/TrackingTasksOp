using System.Text;
using System.Text.Json.Nodes;
using Application.Dto.TimeEntry;
using Application.Ports.UseCases.TimeEntry;
using Infrastructure.Extensions;
using Infrastructure.Settings;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.Adapters.UseCases.TimeEntry;

public class AddTimeEntryImpl(
    IHttpClientFactory httpClientFactory,
    ILogger<AddTimeEntryImpl> logger,
    IOptions<OpenProjectSettings> settings) : IAddTimeEntry
{
    private readonly OpenProjectSettings _settings = settings.Value;
    private readonly HttpClient _client = httpClientFactory.CreateClient(settings.Value.HttpClientName);
    
    public async Task Execute(AddTimeEntryRequest request)
    {
        logger.LogInformation("Executing use case AddTimeEntry");
        var url = BuildUrl();
        var payload = BuildPayload(request);
        var response = await _client.PostAsync(url, payload);

        var jsonResponse = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
            throw new Exception($"Error HTTP {(int)response.StatusCode}: {jsonResponse}");
    }

    private string BuildUrl()
    {
        return $"{_settings.BaseUrl}/api/v3/time_entries";
    }

    private StringContent BuildPayload(AddTimeEntryRequest request)
    {
        var payload = new JsonObject
        {
            ["hours"] = request.Hours.ToIso8601Duration(),
            ["comment"] = new JsonObject
            {
                ["raw"] = request.Comment,  
            },
            ["spentOn"] = DateTime.Now.Date.ToString("yyyy-MM-dd"),
            ["_links"] = new JsonObject
            {
                ["workPackage"] = new JsonObject
                {
                    ["href"] = $"/api/v3/work_packages/{request.IdWorkPackage}"
                },
                ["activity"] = new JsonObject
                {
                    ["href"] = $"/api/v3/time_entries/activities/{request.IdActivity}"
                }
            }
        };
        
        return new StringContent(payload.ToJsonString(), Encoding.UTF8, "application/json");
    }
}