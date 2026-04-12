using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Application.Dto.WorkPackages;
using Application.Ports.UseCases.WorkPackages;
using Domain.Entities.OpenProjectEntities.WorkPackage;
using Microsoft.Extensions.Options;
using Web.Infrastructure.Config.Extensions;
using Web.Infrastructure.Config.Settings;

namespace Web.Infrastructure.Adapters.UseCases.WorkPackages;

public class CreateWorkPackageCommandImpl(
    IHttpClientFactory httpClientFactory,
    ILogger<CreateWorkPackageCommandImpl> logger,
    IOptions<OpenProjectSettings> settings
    ) : ICreateWorkPackageCommand
{
    private readonly OpenProjectSettings _settings = settings.Value;
    private readonly HttpClient _client = httpClientFactory.CreateClient(settings.Value.HttpClientName);

    public async Task<WorkPackage> Execute(CreateWorkPackageRequest request)
    {
        logger.LogInformation("Executing CreateWorkPackageCommand for subject: {Subject}", request.Subject);
        
        string url = $"{_settings.BaseUrl}/api/v3/projects/{request.ProjectId}/work_packages";
        
        var payload = new JsonObject
        {
            ["subject"] = request.Subject,
            ["description"] = new JsonObject
            {
                ["format"] = "markdown",
                ["raw"] = request.Description ?? string.Empty
            }
        };

        var links = new JsonObject();
        
        if (request.StatusId.HasValue && request.StatusId.Value > 0)
        {
            links["status"] = new JsonObject { ["href"] = $"/api/v3/statuses/{request.StatusId}" };
        }
        
        if (request.TypeId.HasValue && request.TypeId.Value > 0)
        {
            links["type"] = new JsonObject { ["href"] = $"/api/v3/types/{request.TypeId}" };
        }
        else
        {
            // Intentar usar un tipo por defecto (por ejemplo 1: Task) si no se especifica
            links["type"] = new JsonObject { ["href"] = $"/api/v3/types/1" };
        }
        
        if (request.PriorityId.HasValue && request.PriorityId.Value > 0)
        {
            links["priority"] = new JsonObject { ["href"] = $"/api/v3/priorities/{request.PriorityId}" };
        }

        if (request.AssigneeId.HasValue && request.AssigneeId.Value > 0)
        {
            links["assignee"] = new JsonObject { ["href"] = $"/api/v3/users/{request.AssigneeId}" };
        }

        if (request.ResponsibleId.HasValue && request.ResponsibleId.Value > 0)
        {
            links["responsible"] = new JsonObject { ["href"] = $"/api/v3/users/{request.ResponsibleId}" };
        }

        payload["_links"] = links;

        var content = new StringContent(payload.ToJsonString(), Encoding.UTF8, "application/json");
        
        var response = await _client.PostAsync(url, content);
        var jsonResponse = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            logger.LogError("Error creating work package: {Response}", jsonResponse);
            throw new Exception($"Error HTTP {(int)response.StatusCode}: {jsonResponse}");
        }

        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var workPackage = JsonSerializer.Deserialize<WorkPackage>(jsonResponse, options);

        return workPackage ?? throw new Exception("Failed to deserialize created work package.");
    }
}
