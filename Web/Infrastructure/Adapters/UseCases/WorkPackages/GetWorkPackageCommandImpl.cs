using System.Net;
using System.Text.Json;
using Application.Ports.UseCases.WorkPackages;
using Domain.Entities.OpenProjectEntities.WorkPackage;
using Microsoft.Extensions.Options;
using Web.Infrastructure.Config.Settings;

namespace Web.Infrastructure.Adapters.UseCases.WorkPackages;

public class GetWorkPackageCommandImpl(
    IHttpClientFactory httpClientFactory,
    ILogger<GetWorkPackageCommandImpl> logger,
    IOptions<OpenProjectSettings> settings
    ) : IGetWorkPackageCommand
{
    private readonly OpenProjectSettings _settings = settings.Value;
    private readonly HttpClient _client = httpClientFactory.CreateClient(settings.Value.HttpClientName);

    public async Task<WorkPackage?> Execute(int id)
    {
        logger.LogInformation("Executing GetWorkPackageCommand for ID {Id}", id);
        
        string url = $"{_settings.BaseUrl}/api/v3/work_packages/{id}";
        HttpResponseMessage response = await _client.GetAsync(url);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        if (!response.IsSuccessStatusCode)
        {
            string error = await response.Content.ReadAsStringAsync();
            throw new Exception($"Error HTTP {(int)response.StatusCode}: {error}");
        }

        string json = await response.Content.ReadAsStringAsync();
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        return JsonSerializer.Deserialize<WorkPackage>(json, options);
    }
}
