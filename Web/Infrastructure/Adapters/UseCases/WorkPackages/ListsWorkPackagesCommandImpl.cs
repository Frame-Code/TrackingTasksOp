using System.Net;
using System.Text.Json;
using Application.Dto.ListWorkPackages;
using Application.Ports.UseCases.WorkPackages;
using Domain.Entities.OpenProjectEntities;
using Domain.Entities.OpenProjectEntities.WorkPackage;
using Microsoft.Extensions.Options;
using Web.Infrastructure.Config.Extensions;
using Web.Infrastructure.Config.Settings;

namespace Web.Infrastructure.Adapters.UseCases.WorkPackages;
public class ListsWorkPackagesCommandImpl(
    IHttpClientFactory httpClientFactory,
    ILogger<ListsWorkPackagesCommandImpl> logger,
    IOptions<OpenProjectSettings> settings
    ) : IListsWorkPackagesCommand
{
    private readonly OpenProjectSettings _settings = settings.Value;
    private readonly HttpClient _client = httpClientFactory.CreateClient(settings.Value.HttpClientName);
    
    //Listar de manera paginada todos los work packages
    public async Task<List<WorkPackage>> Execute(ListsWorkPackagesRequest request)
    {
        int pageSize = request.pageSize > 50 ? 50 : request.pageSize;
        int offset = request.offset is < 0 or > 50 ? 0 : request.offset;
        var allItems = new List<WorkPackage>();
        
        logger.LogInformation("Executing ListsWorkPackagesCommand, offset={Offset}, pageSize={PageSize}", offset, pageSize);   
        
        string url = BuildUrl(request.ProjectId, offset, pageSize);
        HttpResponseMessage  response = await _client.GetAsync(url);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return allItems;
        
        if (!response.IsSuccessStatusCode)
        {
            string error = await response.Content.ReadAsStringAsync();
            throw new Exception($"Error HTTP {(int)response.StatusCode}: {error}");
        }
        
        string json = await response.Content.ReadAsStringAsync();
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var collection = JsonSerializer.Deserialize<WorkPackageCollection>(json, options);

        if (collection?.Embedded?.Elements != null && collection.Embedded.Elements.Count > 0)
        {
            allItems.AddRange(collection.Embedded.Elements); 
        }

        return allItems;
    }
    
    private string BuildUrl(int? projectId, int offset, int pageSize)
    {
        string baseEndpoint = projectId.HasValue
            ? $"{_settings.BaseUrl}/api/v3/projects/{projectId}/work_packages"
            : $"{_settings.BaseUrl}/api/v3/work_packages";

        string filters = Uri.EscapeDataString("[{\"assignee\":{\"operator\":\"=\",\"values\":[\"me\"]}},{\"status\":{\"operator\":\"o\",\"values\":[]}}]");
        return $"{baseEndpoint}?filters={filters}&offset={offset}&pageSize={pageSize}";
    }
}
