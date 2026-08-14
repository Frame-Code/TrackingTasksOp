using System.Text;
using System.Text.Json.Nodes;
using Application.Dto.TimeEntry;
using Application.Ports.UseCases.TimeEntry;
using Infrastructure.Extensions;
using Infrastructure.Settings;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.Adapters.UseCases.TimeEntry;

public class AddTimeEntryCommandImpl(
    IHttpClientFactory httpClientFactory,
    ILogger<AddTimeEntryCommandImpl> logger) : IAddTimeEntryCommand
{
    private readonly HttpClient _client = httpClientFactory.CreateClient(KeyedServicesNames.OpenProjectHttpClientName);
    
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
        return $"/api/v3/time_entries";
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
            ["spentOn"] = (request.SpentOn ?? DateOnly.FromDateTime(DateTime.Now)).ToString("yyyy-MM-dd"),
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