using System.Text.Json;
using Application.Ports.UseCases.WorkPackages;
using Domain.Entities.OpenProjectEntities.WorkPackage;
using Infrastructure.Settings;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Adapters.UseCases.WorkPackages;

public class GetWorkPackageChildrenQueryImpl(
    IHttpClientFactory httpClientFactory,
    ILogger<GetWorkPackageChildrenQueryImpl> logger) : IGetWorkPackageChildrenQuery
{
    private readonly HttpClient _client = httpClientFactory.CreateClient(KeyedServicesNames.OpenProjectHttpClientName);

    /// <summary>
    /// Un nodo del árbol difícilmente tenga más hijos que esto, y pedir de más cuesta:
    /// OpenProject cobra ~30 ms por work package serializado.
    /// </summary>
    private const int PageSize = 100;

    public async Task<List<WorkPackage>> ExecuteAsync(int parentId, CancellationToken ct = default)
    {
        if (parentId <= 0) return [];

        // Sin filtro de asignado A PROPÓSITO: es el corazón del árbol. Si alguien reusa por
        // comodidad el builder del listado (que filtra por "me"), el árbol pasa a mostrar
        // solo las tareas propias y el requerimiento se rompe en silencio.
        string filters = Uri.EscapeDataString($"[{{\"parent\":{{\"operator\":\"=\",\"values\":[\"{parentId}\"]}}}}]");
        string sortBy = Uri.EscapeDataString("[[\"id\",\"asc\"]]");
        string url = $"/api/v3/work_packages?filters={filters}&pageSize={PageSize}&sortBy={sortBy}";

        var response = await _client.GetAsync(url, ct);
        string json = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            logger.LogError("Error fetching children of {ParentId}: {Response}", parentId, json);
            throw new Exception(OpenProjectError.ExtractMessage(json));
        }

        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var collection = JsonSerializer.Deserialize<WorkPackageCollection>(json, options);

        return collection?.Embedded?.Elements ?? [];
    }
}
