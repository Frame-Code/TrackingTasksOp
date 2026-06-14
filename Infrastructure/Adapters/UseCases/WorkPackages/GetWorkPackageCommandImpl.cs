using System.Net;
using System.Text.Json;
using Application.Ports.UseCases.WorkPackages;
using Domain.Entities.OpenProjectEntities.WorkPackage;
using Infrastructure.Settings;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Adapters.UseCases.WorkPackages;

public class GetWorkPackageCommandImpl(
    IHttpClientFactory httpClientFactory,
    ILogger<GetWorkPackageCommandImpl> logger) : IGetWorkPackageCommand
{
    private readonly HttpClient _client = httpClientFactory.CreateClient(KeyedServicesNames.OpenProjectHttpClientName);

    public async Task<WorkPackage?> Execute(int workPackageId)
    {
        var response = await _client.GetAsync($"/api/v3/work_packages/{workPackageId}");

        if (response.StatusCode == HttpStatusCode.NotFound) return null;

        var json = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            logger.LogError("Error fetching work package {Id}: {Response}", workPackageId, json);
            throw new Exception($"Error HTTP {(int)response.StatusCode}: {json}");
        }

        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        return JsonSerializer.Deserialize<WorkPackage>(json, options);
    }
}
