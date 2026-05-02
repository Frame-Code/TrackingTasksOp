using System.Text;
using System.Text.Json.Nodes;
using Application.Ports.UseCases.WorkPackages;
using Infrastructure.Settings;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.Adapters.UseCases.WorkPackages;

public class UpdateWorkPackageCommandImpl(
    IHttpClientFactory httpClientFactory,
    ILogger<UpdateWorkPackageCommandImpl> logger,
    IOptions<OpenProjectSettings> settings
    ) : IUpdateWorkPackageCommand
{
    private readonly OpenProjectSettings _settings = settings.Value;
    private readonly HttpClient _client = httpClientFactory.CreateClient(settings.Value.HttpClientName);

    public async Task Execute(int workPackageId, int? statusId = null, int? assigneeId = null, int? responsibleId = null)
    {
        logger.LogInformation("Updating work package {WpId}. Status: {StatusId}, Assignee: {AssigneeId}, Responsible: {ResponsibleId}", 
            workPackageId, statusId, assigneeId, responsibleId);
        
        int lockVersion = await GetLockVersion(workPackageId);

        string url = $"{_settings.BaseUrl}/api/v3/work_packages/{workPackageId}";
        
        var links = new JsonObject();
        
        if (statusId.HasValue && statusId.Value > 0)
        {
            links["status"] = new JsonObject { ["href"] = $"/api/v3/statuses/{statusId}" };
        }

        if (assigneeId.HasValue)
        {
            links["assignee"] = assigneeId.Value > 0 
                ? new JsonObject { ["href"] = $"/api/v3/users/{assigneeId}" }
                : null; // null en OP API remueve la asignación
        }

        if (responsibleId.HasValue)
        {
            links["responsible"] = responsibleId.Value > 0 
                ? new JsonObject { ["href"] = $"/api/v3/users/{responsibleId}" }
                : null;
        }

        var payload = new JsonObject
        {
            ["lockVersion"] = lockVersion,
            ["_links"] = links
        };

        var content = new StringContent(payload.ToJsonString(), Encoding.UTF8, "application/json");
        
        var request = new HttpRequestMessage(new HttpMethod("PATCH"), url) { Content = content };
        var response = await _client.SendAsync(request);
        
        if (!response.IsSuccessStatusCode)
        {
            var jsonResponse = await response.Content.ReadAsStringAsync();
            logger.LogError("Error updating work package: {Response}", jsonResponse);
            throw new Exception($"Error HTTP {(int)response.StatusCode}: {jsonResponse}");
        }
    }

    private async Task<int> GetLockVersion(int workPackageId)
    {
        string url = $"{_settings.BaseUrl}/api/v3/work_packages/{workPackageId}";
        var response = await _client.GetAsync(url);
        if (!response.IsSuccessStatusCode) throw new Exception($"Could not fetch work package {workPackageId}");
        var json = await response.Content.ReadAsStringAsync();
        var node = JsonNode.Parse(json);
        return node?["lockVersion"]?.GetValue<int>() ?? 0;
    }
}
