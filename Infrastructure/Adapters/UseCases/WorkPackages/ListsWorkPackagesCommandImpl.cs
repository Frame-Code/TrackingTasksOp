using System.Net;
using System.Text.Json;
using Application.Dto.ListWorkPackages;
using Application.Ports.UseCases.WorkPackages;
using Domain.Entities.OpenProjectEntities.WorkPackage;
using Infrastructure.Settings;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.Adapters.UseCases.WorkPackages;
public class ListsWorkPackagesCommandImpl(
    IHttpClientFactory httpClientFactory,
    ILogger<ListsWorkPackagesCommandImpl> logger
    ) : IListsWorkPackagesCommand
{
    private readonly HttpClient _client = httpClientFactory.CreateClient(KeyedServicesNames.OpenProjectHttpClientName);
    
    //Listar de manera paginada todos los work packages
    public async Task<List<WorkPackage>> Execute(ListsWorkPackagesRequest request)
    {
        int pageSize = request.pageSize > 50 ? 50 : request.pageSize;
        // En OpenProject `offset` es el NUMERO DE PAGINA, no un desplazamiento de elementos.
        // Antes se avanzaba de a pageSize (1, 51, 101...) y se saltaban paginas enteras,
        // asi que faltaban tareas cuando habia mas de una pagina.
        int page = request.offset <= 0 ? 1 : request.offset;
        var allItems = new List<WorkPackage>();
        int total;
        
        logger.LogInformation("Executing ListsWorkPackagesCommand, page={Page}, pageSize={PageSize}", page, pageSize);   
        do
        {
            string url = BuildUrl(request.ProjectId, page, pageSize, request.StatusId, request.OnlyOpen);
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
            page++;
        } while (allItems.Count < total);

        return allItems;
    }
    
    public async Task<List<WorkPackage>> GetByIdsAsync(IReadOnlyCollection<int> ids, CancellationToken ct = default)
    {
        var result = new List<WorkPackage>();
        if (ids.Count == 0) return result;

        // OpenProject acota el pageSize, así que se pide en tandas en vez de una sola URL enorme
        // (que además podría exceder el largo máximo de query string).
        const int batchSize = 100;
        foreach (var batch in ids.Distinct().Chunk(batchSize))
        {
            string values = string.Join(",", batch.Select(id => $"\"{id}\""));
            string filters = Uri.EscapeDataString($"[{{\"id\":{{\"operator\":\"=\",\"values\":[{values}]}}}}]");
            string url = $"/api/v3/work_packages?filters={filters}&pageSize={batchSize}";

            var response = await _client.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode)
            {
                // El reporte no debe caerse porque no se pudo enriquecer con datos de personas:
                // se registra el fallo y esas columnas quedan vacías.
                logger.LogError("Error fetching work packages by id: HTTP {Status}", (int)response.StatusCode);
                continue;
            }

            string json = await response.Content.ReadAsStringAsync(ct);
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var collection = JsonSerializer.Deserialize<WorkPackageCollection>(json, options);

            if (collection?.Embedded?.Elements is { Count: > 0 } elements)
                result.AddRange(elements);
        }

        return result;
    }

    private string BuildUrl(int? projectId, int page, int pageSize, int? statusId, bool onlyOpen = false)
    {
        string baseEndpoint = projectId.HasValue
            ? $"/api/v3/projects/{projectId}/work_packages"
            : $"/api/v3/work_packages";

        // "=" un estado concreto, "o" solo abiertas, "*" todos.
        // El default es "*": con "o" OpenProject omitia las tareas cerradas y faltaban en la UI.
        string statusFilter = statusId.HasValue
            ? $"{{\"status\":{{\"operator\":\"=\",\"values\":[\"{statusId.Value}\"]}}}}"
            : onlyOpen
                ? "{\"status\":{\"operator\":\"o\",\"values\":[]}}"
                : "{\"status\":{\"operator\":\"*\",\"values\":[]}}";
        string filters = Uri.EscapeDataString($"[{{\"assignee\":{{\"operator\":\"=\",\"values\":[\"me\"]}}}},{statusFilter}]");
        string sortBy = Uri.EscapeDataString("[[\"createdAt\",\"desc\"]]");
        return $"{baseEndpoint}?filters={filters}&offset={page}&pageSize={pageSize}&sortBy={sortBy}";
    }
}
