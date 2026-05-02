using System.Net;
using System.Text.Json;
using Application.Dto.ListWorkPackages;
using Application.Ports.UseCases.WorkPackages;
using Domain.Entities.OpenProjectEntities.WorkPackage;
using Microsoft.Extensions.Options;
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
        int offset = request.offset <= 0 ? 1 : request.offset;
        var allItems = new List<WorkPackage>();
        int total;
        
        logger.LogInformation("Executing ListsWorkPackagesCommand, offset={Offset}, pageSize={PageSize}", offset, pageSize);   
        do
        {
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

            if (collection?.Embedded?.Elements == null || collection?.Embedded?.Elements.Count == 0)
                break;
            
            allItems.AddRange(collection!.Embedded!.Elements);
            total = collection.Total;
            offset += pageSize;
        } while (allItems.Count < total);

        return allItems;
    }
    
    private string BuildUrl(int? projectId, int offset, int pageSize)
    {
        string baseEndpoint = projectId.HasValue
            ? $"{_settings.BaseUrl}/api/v3/projects/{projectId}/work_packages"
            : $"{_settings.BaseUrl}/api/v3/work_packages";

        string filters = Uri.EscapeDataString("[{\"assignee\":{\"operator\":\"=\",\"values\":[\"me\"]}},{\"status\":{\"operator\":\"o\",\"values\":[]}}]");
        string sortBy = Uri.EscapeDataString("[[\"createdAt\",\"desc\"]]");
        return $"{baseEndpoint}?filters={filters}&offset={offset}&pageSize={pageSize}&sortBy={sortBy}";
    }
}
