using System.Net;
using System.Text.Json;
using Application.Dto.ListWorkPackages;
using Application.Services.UseCases.WorkPackages;
using Domain.Entities.OpenProjectEntities;
using Web.Infrastructure.Config.Extensions;
using Web.Infrastructure.Config.Settings;

namespace Web.Infrastructure.Services.UseCases.WorkPackages;
public class ListsWorkPackagesCommandImpl(
    IHttpClientFactory httpClientFactory,
    ILogger<ListsWorkPackagesCommandImpl> logger,
    [FromKeyedServices(nameof(KeyService.OpenProjectSettings))]
    IApiSettings settings
    ) : IListsWorkPackagesCommand
{
    private readonly HttpClient _client = httpClientFactory.CreateClient(settings.GetHttpClientName());
    
    //Listar de manera paginada todos los work packages
    public async Task<List<WorkPackage>> Execute(ListsWorkPackagesRequest request)
    {
        int pageSize = request.pageSize > 50 ? 50 : request.pageSize;
        int offset = request.offset is < 0 or > 50 ? 0 : request.offset;
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
            offset += collection.Count + 1; 
        } while (allItems.Count <= total);

        return allItems;
    }
    
    private string BuildUrl(int? projectId, int offset, int pageSize)
    {
        string baseEndpoint = projectId.HasValue
            ? $"{settings.GetUri()}/api/v3/projects/{projectId}/work_packages"
            : $"{settings.GetUri()}/api/v3/work_packages";

        return $"{baseEndpoint}?offset={offset}&pageSize={pageSize}";
    }
}
