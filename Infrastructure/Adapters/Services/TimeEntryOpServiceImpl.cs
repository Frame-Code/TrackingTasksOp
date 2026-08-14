using System.Net;
using System.Text.Json;
using Application.Ports.Services;
using Domain.Entities.OpenProjectEntities.TimeEntries;
using Infrastructure.Settings;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Adapters.Services;

public class TimeEntryOpServiceImpl(
    IHttpClientFactory httpClientFactory,
    ILogger<TimeEntryOpServiceImpl> logger) : ITimeEntryOpService
{
    private const int PageSize = 200;

    private readonly HttpClient _client = httpClientFactory.CreateClient(KeyedServicesNames.OpenProjectHttpClientName);

    public async Task<List<OpTimeEntry>> Lists(DateOnly from, DateOnly to, int? userId = null)
    {
        logger.LogInformation("Executing Lists:TimeEntryOpService {From}..{To} user={UserId}", from, to, userId);

        var all = new List<OpTimeEntry>();
        var offset = 1;   // OpenProject pagina desde 1, no desde 0

        while (true)
        {
            var url = BuildUrl(from, to, userId, offset);
            var response = await _client.GetAsync(url);

            if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Unauthorized)
                return all;

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new Exception($"Error HTTP {(int)response.StatusCode}: {error}");
            }

            var json = await response.Content.ReadAsStringAsync();
            var collection = JsonSerializer.Deserialize<TimeEntryCollection>(
                json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            var elements = collection?.Embedded?.Elements ?? [];
            all.AddRange(elements);

            // Cortamos cuando la página vino incompleta o ya juntamos el total anunciado.
            if (elements.Count < PageSize || (collection is not null && all.Count >= collection.Total))
                return all;

            offset++;
        }
    }

    private static string BuildUrl(DateOnly from, DateOnly to, int? userId, int offset)
    {
        var filters = $"[{{\"spentOn\":{{\"operator\":\"<>d\",\"values\":[\"{from:yyyy-MM-dd}\",\"{to:yyyy-MM-dd}\"]}}}}";
        if (userId is > 0)
            filters += $",{{\"user\":{{\"operator\":\"=\",\"values\":[\"{userId}\"]}}}}";
        filters += "]";

        return $"/api/v3/time_entries?filters={Uri.EscapeDataString(filters)}&pageSize={PageSize}&offset={offset}";
    }
}
