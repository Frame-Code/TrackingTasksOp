using System.Text.Json;
using Application.Dto.ListWorkPackages;
using Application.Services.UseCases.WorkPackages;
using Domain.Entities.OpenProjectEntities;
using Web.Infrastructure.Config.Extensions;
using Web.Infrastructure.Config.Settings;

namespace Web.Infrastructure.Services.UseCases.WorkPackages;
public class ListsWorkPackagesCommandImpl(
    IHttpClientFactory httpClientFactory, 
    [FromKeyedServices(nameof(KeyService.OpenProjectSettings))]
    IApiSettings settings) : IListsWorkPackagesCommand
{
    private readonly int _pageSize = 50;
    private readonly HttpClient _client = httpClientFactory.CreateClient(settings.GetHttpClientName());

    public async Task<List<WorkPackage>> Execute(ListWorkPackagesRequest resquest)
    {
        var allItems = new List<WorkPackage>();
        var offset = 1;
        int total;

        do
        {
            string url = BuildUrl(resquest.ProjectId, offset, _pageSize);
            HttpResponseMessage  response = await _client.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                string error = await response.Content.ReadAsStringAsync();
                throw new Exception($"Error HTTP {(int)response.StatusCode}: {error}");
            }
            
            string json = await response.Content.ReadAsStringAsync();
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var collection = JsonSerializer.Deserialize<WorkPackageCollection>(json, options);

            if (collection?.Embedded?.Elements == null)
                break;
            
            allItems.AddRange(collection.Embedded.Elements);
            total = collection.Total;
            offset += _pageSize;
        } while (allItems.Count < total);

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
