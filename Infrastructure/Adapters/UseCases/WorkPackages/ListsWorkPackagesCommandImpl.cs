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
    ILogger<ListsWorkPackagesCommandImpl> logger,
    Infrastructure.Adapters.Http.RequestTimings timings
    ) : IListsWorkPackagesCommand
{
    private readonly HttpClient _client = httpClientFactory.CreateClient(KeyedServicesNames.OpenProjectHttpClientName);
    
    //Listar de manera paginada todos los work packages
    public async Task<List<WorkPackage>> Execute(ListsWorkPackagesRequest request)
    {
        // OpenProject topa el pageSize en 100; pedir de a 100 en vez de 50 parte a la mitad
        // la cantidad de peticiones necesarias.
        int pageSize = Math.Clamp(request.pageSize <= 0 ? MaxPageSize : request.pageSize, 1, MaxPageSize);
        // En OpenProject `offset` es el NUMERO DE PAGINA, no un desplazamiento de elementos.
        // Antes se avanzaba de a pageSize (1, 51, 101...) y se saltaban paginas enteras,
        // asi que faltaban tareas cuando habia mas de una pagina.
        int firstPage = request.offset <= 0 ? 1 : request.offset;

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        var first = await FetchPageAsync(request, firstPage, pageSize);

        if (first?.Embedded?.Elements is not { Count: > 0 } firstElements)
            return [];

        var allItems = new List<WorkPackage>(firstElements);

        int totalPages = (int)Math.Ceiling(first.Total / (double)pageSize);
        if (totalPages <= 1)
        {
            timings.Add("op-total", stopwatch.ElapsedMilliseconds);
            logger.LogInformation(
                "ListsWorkPackages: {Count} tareas en {Elapsed} ms (1 pagina de {PageSize})",
                allItems.Count, stopwatch.ElapsedMilliseconds, pageSize);
            return allItems;
        }

        // Las páginas restantes se piden EN PARALELO. En serie eran N viajes encadenados
        // contra OpenProject y la carga inicial se sentía lenta aunque cada petición
        // individual fuese rápida.
        var rest = await System.Threading.Tasks.Task.WhenAll(
            Enumerable.Range(firstPage + 1, totalPages - 1)
                      .Select(p => FetchPageAsync(request, p, pageSize)));

        foreach (var collection in rest)
            if (collection?.Embedded?.Elements is { Count: > 0 } elements)
                allItems.AddRange(elements);

        // Si este total se parece a la SUMA de las páginas, OpenProject las está atendiendo
        // en serie (worker único) y paralelizar no sirve: hay que pedir MENOS, no a la vez.
        // Si se parece a la página MÁS LENTA, el paralelismo funciona y el cuello está en
        // cuánto tarda OpenProject en resolver una sola consulta.
        var totalElapsed = stopwatch.ElapsedMilliseconds;
        timings.Add("op-total", totalElapsed);
        logger.LogInformation(
            "ListsWorkPackages: {Count} tareas en {Elapsed} ms ({Pages} paginas de {PageSize}, 1 secuencial + {Parallel} en paralelo)",
            allItems.Count, totalElapsed, totalPages, pageSize, totalPages - 1);

        return allItems;
    }

    /// <summary>
    /// Una sola página. OpenProject cobra ~30 ms por work package serializado, así que
    /// pedir 12 en vez de 209 es la diferencia entre ~1 s y ~9 s. Por eso el filtro de
    /// estado y la búsqueda se resuelven allá y no en el navegador: si se filtrara en el
    /// cliente habría que traerlo todo igual y no se ganaría nada.
    /// </summary>
    public async Task<PagedWorkPackages<WorkPackage>> ExecutePageAsync(ListsWorkPackagesRequest request)
    {
        int pageSize = Math.Clamp(request.pageSize <= 0 ? 12 : request.pageSize, 1, MaxPageSize);
        int page = request.offset <= 0 ? 1 : request.offset;

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var collection = await FetchPageAsync(request, page, pageSize);
        timings.Add("op-total", stopwatch.ElapsedMilliseconds);

        var items = collection?.Embedded?.Elements ?? [];
        logger.LogInformation(
            "ListsWorkPackages (pagina {Page}): {Count} de {Total} tareas en {Elapsed} ms",
            page, items.Count, collection?.Total ?? 0, stopwatch.ElapsedMilliseconds);

        return new PagedWorkPackages<WorkPackage>(items, collection?.Total ?? 0, page, pageSize);
    }

    private const int MaxPageSize = 100;

    private async System.Threading.Tasks.Task<WorkPackageCollection?> FetchPageAsync(
        ListsWorkPackagesRequest request, int page, int pageSize)
    {
        string url = BuildUrl(request, page, pageSize);

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        HttpResponseMessage response = await _client.GetAsync(url);

        // Se mide solo el viaje a OpenProject, no la deserializacion: es lo que hay que
        // saber para decidir si el cuello esta alla o de este lado.
        var elapsed = stopwatch.ElapsedMilliseconds;
        timings.Add($"op-pagina-{page}", elapsed);
        logger.LogInformation(
            "  └ OpenProject pagina {Page}: {Elapsed} ms (HTTP {Status})",
            page, elapsed, (int)response.StatusCode);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        if (!response.IsSuccessStatusCode)
        {
            string error = await response.Content.ReadAsStringAsync();
            throw new Exception($"Error HTTP {(int)response.StatusCode}: {error}");
        }

        string json = await response.Content.ReadAsStringAsync();
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        return JsonSerializer.Deserialize<WorkPackageCollection>(json, options);
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

    private string BuildUrl(ListsWorkPackagesRequest request, int page, int pageSize)
    {
        string baseEndpoint = request.ProjectId.HasValue
            ? $"/api/v3/projects/{request.ProjectId}/work_packages"
            : $"/api/v3/work_packages";

        var filterParts = new List<string>();
        if (request.OnlyMine)
            filterParts.Add("{\"assignee\":{\"operator\":\"=\",\"values\":[\"me\"]}}");

        // Varios estados (pildoras de la UI) > un estado > solo abiertas > todos.
        // El default es "*": con "o" OpenProject omitia las tareas cerradas y faltaban en la UI.
        var statusIds = request.StatusIds is { Count: > 0 }
            ? request.StatusIds
            : request.StatusId.HasValue ? new[] { request.StatusId.Value } : null;

        filterParts.Add(statusIds is not null
            ? $"{{\"status\":{{\"operator\":\"=\",\"values\":[{string.Join(",", statusIds.Select(id => $"\"{id}\""))}]}}}}"
            : request.OnlyOpen
                ? "{\"status\":{\"operator\":\"o\",\"values\":[]}}"
                : "{\"status\":{\"operator\":\"*\",\"values\":[]}}");

        // Buscar en el servidor y no en el navegador: es lo que permite no traerlo todo.
        // "~" es "contiene" en OpenProject; las comillas se escapan para no romper el JSON.
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim().Replace("\\", "\\\\").Replace("\"", "\\\"");
            filterParts.Add($"{{\"subjectOrId\":{{\"operator\":\"**\",\"values\":[\"{term}\"]}}}}");
        }

        string filters = Uri.EscapeDataString($"[{string.Join(",", filterParts)}]");
        string sortBy = Uri.EscapeDataString("[[\"createdAt\",\"desc\"]]");
        string url = $"{baseEndpoint}?filters={filters}&offset={page}&pageSize={pageSize}&sortBy={sortBy}";

        // Sin `select`: se probo para aligerar la respuesta, pero esta instancia de
        // OpenProject no reconoce esas rutas (devolvia los elementos vacios) y costaba un
        // viaje extra en CADA request. Pidiendo una sola pagina ya no hace falta.
        return $"{baseEndpoint}?filters={filters}&offset={page}&pageSize={pageSize}&sortBy={sortBy}";
    }
}
