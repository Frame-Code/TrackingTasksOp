using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Application.Ports.UseCases.WorkPackages;
using Infrastructure.Exceptions;
using Infrastructure.Settings;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Adapters.UseCases.WorkPackages;

public class UpdateWorkPackageCommandImpl(
    IHttpClientFactory httpClientFactory,
    ILogger<UpdateWorkPackageCommandImpl> logger
    ) : IUpdateWorkPackageCommand
{
    private readonly HttpClient _client = httpClientFactory.CreateClient(KeyedServicesNames.OpenProjectHttpClientName);

    /// <summary>Sentinel: pasar este valor como startDate/dueDate significa "no tocar el campo".</summary>
    public const string NoChange = "__no_change__";

    public async Task Execute(
        int     workPackageId,
        int?    statusId        = null,
        int?    assigneeId      = null,
        int?    responsibleId   = null,
        int?    percentageDone  = null,
        string? startDate       = NoChange,
        string? dueDate         = NoChange)
    {
        logger.LogInformation(
            "Updating work package {WpId}. Status:{S} Assignee:{A} Responsible:{R} Progress:{P} Start:{SD} Due:{DD}",
            workPackageId, statusId, assigneeId, responsibleId, percentageDone, startDate, dueDate);

        int lockVersion = await GetLockVersion(workPackageId);

        string url = $"/api/v3/work_packages/{workPackageId}";

        var payload = new JsonObject { ["lockVersion"] = lockVersion };

        // ── Campos escalares ────────────────────────────────────────────────────
        if (percentageDone.HasValue)
            payload["percentageDone"] = percentageDone.Value;

        // startDate / dueDate: string "yyyy-MM-dd" para fijar, "" o null para limpiar.
        // El sentinel UpdateWorkPackageCommandImpl.NoChange indica que el campo no debe tocarse.
        if (startDate != NoChange)
            payload["startDate"] = string.IsNullOrEmpty(startDate) ? null : (JsonNode)startDate!;

        if (dueDate != NoChange)
            payload["dueDate"] = string.IsNullOrEmpty(dueDate) ? null : (JsonNode)dueDate!;

        // ── Links ───────────────────────────────────────────────────────────────
        var links = new JsonObject();

        if (statusId.HasValue && statusId.Value > 0)
            links["status"] = new JsonObject { ["href"] = $"/api/v3/statuses/{statusId}" };

        if (assigneeId.HasValue)
            links["assignee"] = assigneeId.Value > 0
                ? new JsonObject { ["href"] = $"/api/v3/users/{assigneeId}" }
                : null;

        if (responsibleId.HasValue)
            links["responsible"] = responsibleId.Value > 0
                ? new JsonObject { ["href"] = $"/api/v3/users/{responsibleId}" }
                : null;

        if (links.Count > 0)
            payload["_links"] = links;

        var content = new StringContent(payload.ToJsonString(), Encoding.UTF8, "application/json");
        var request = new HttpRequestMessage(new HttpMethod("PATCH"), url) { Content = content };
        var response = await _client.SendAsync(request);

        if (!response.IsSuccessStatusCode)
        {
            var jsonResponse = await response.Content.ReadAsStringAsync();
            logger.LogError("Error updating work package: {Response}", jsonResponse);

            if (statusId.HasValue && IsStatusConstraintViolation(jsonResponse))
            {
                var allowedStatuses = await GetAllowedStatusNames(workPackageId, lockVersion);
                throw new InvalidStatusTransitionException(ExtractMessage(jsonResponse), allowedStatuses);
            }

            throw new Exception($"Error HTTP {(int)response.StatusCode}: {jsonResponse}");
        }
    }

    private static bool IsStatusConstraintViolation(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.TryGetProperty("_embedded", out var embedded)
                && embedded.TryGetProperty("details", out var details)
                && details.TryGetProperty("attribute", out var attribute)
                && attribute.GetString() == "status";
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string ExtractMessage(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.TryGetProperty("message", out var message) ? message.GetString() ?? json : json;
        }
        catch (JsonException)
        {
            return json;
        }
    }

    private async Task<List<string>> GetAllowedStatusNames(int workPackageId, int lockVersion)
    {
        var payload = new JsonObject { ["lockVersion"] = lockVersion };
        var content = new StringContent(payload.ToJsonString(), Encoding.UTF8, "application/json");
        var response = await _client.PostAsync($"/api/v3/work_packages/{workPackageId}/form", content);
        if (!response.IsSuccessStatusCode) return new List<string>();

        var json = await response.Content.ReadAsStringAsync();
        try
        {
            using var doc = JsonDocument.Parse(json);
            var allowedValues = doc.RootElement
                .GetProperty("_embedded").GetProperty("schema")
                .GetProperty("status").GetProperty("_embedded").GetProperty("allowedValues");

            return allowedValues.EnumerateArray()
                .Select(v => v.GetProperty("name").GetString() ?? "")
                .Where(name => name.Length > 0)
                .ToList();
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            logger.LogWarning("No se pudieron obtener los estados permitidos para el work package {Id}: {Error}", workPackageId, ex.Message);
            return new List<string>();
        }
    }

    private async Task<int> GetLockVersion(int workPackageId)
    {
        string url = $"/api/v3/work_packages/{workPackageId}";
        var response = await _client.GetAsync(url);
        if (!response.IsSuccessStatusCode) throw new Exception($"Could not fetch work package {workPackageId}");
        var json = await response.Content.ReadAsStringAsync();
        var node = JsonNode.Parse(json);
        return node?["lockVersion"]?.GetValue<int>() ?? 0;
    }
}
