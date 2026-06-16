using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Application.Dto.WorkPackages;
using Application.Ports.UseCases.WorkPackages;
using Domain.Entities.OpenProjectEntities.WorkPackage;
using Infrastructure.Settings;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.Adapters.UseCases.WorkPackages;

public class CreateWorkPackageCommandImpl(
    IHttpClientFactory httpClientFactory,
    ILogger<CreateWorkPackageCommandImpl> logger
    ) : ICreateWorkPackageCommand
{
    private readonly HttpClient _client = httpClientFactory.CreateClient(KeyedServicesNames.OpenProjectHttpClientName);

    public async Task<WorkPackage> Execute(CreateWorkPackageRequest request)
    {
        logger.LogInformation("Executing CreateWorkPackageCommand for subject: {Subject}", request.Subject);
        
        string url = $"/api/v3/projects/{request.ProjectId}/work_packages";
        
        var payload = new JsonObject
        {
            ["subject"] = request.Subject,
            ["description"] = new JsonObject
            {
                ["format"] = "markdown",
                ["raw"] = request.Description ?? string.Empty
            }
        };

        if (request.StartDate.HasValue)
            payload["startDate"] = request.StartDate.Value.ToString("yyyy-MM-dd");

        if (request.DueDate.HasValue)
            payload["dueDate"] = request.DueDate.Value.ToString("yyyy-MM-dd");

        var links = new JsonObject();
        
        if (request.StatusId.HasValue && request.StatusId.Value > 0)
        {
            links["status"] = new JsonObject { ["href"] = $"/api/v3/statuses/{request.StatusId}" };
        }
        
        int typeId = request.TypeId is > 0 ? request.TypeId.Value : await GetDefaultTypeIdAsync(request.ProjectId);
        links["type"] = new JsonObject { ["href"] = $"/api/v3/types/{typeId}" };


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

        if (request.CustomFieldOptionIds != null)
        {
            foreach (var (key, optionId) in request.CustomFieldOptionIds)
            {
                links[key] = new JsonObject { ["href"] = $"/api/v3/custom_options/{optionId}" };
            }
        }

        payload["_links"] = links;

        if (request.CustomFieldTextValues != null)
        {
            foreach (var (key, value) in request.CustomFieldTextValues)
                payload[key] = value;
        }

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

    public async Task<List<RequiredCustomField>> GetRequiredCustomFieldsAsync(int projectId, int? typeId = null)
    {
        int resolvedTypeId = typeId is > 0 ? typeId.Value : await GetDefaultTypeIdAsync(projectId);
        string url = $"/api/v3/projects/{projectId}/work_packages/form";

        var payload = new JsonObject
        {
            ["_links"] = new JsonObject
            {
                ["type"] = new JsonObject { ["href"] = $"/api/v3/types/{resolvedTypeId}" }
            }
        };

        var content = new StringContent(payload.ToJsonString(), Encoding.UTF8, "application/json");
        var response = await _client.PostAsync(url, content);
        var jsonResponse = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            logger.LogError("Error fetching work package form schema: {Response}", jsonResponse);
            return new List<RequiredCustomField>();
        }

        var result = new List<RequiredCustomField>();
        var schema = JsonNode.Parse(jsonResponse)?["_embedded"]?["schema"]?.AsObject();
        if (schema is null) return result;

        var textFieldTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "String", "Text", "Integer", "Float", "Date", "Boolean" };

        foreach (var (key, node) in schema)
        {
            if (!key.StartsWith("customField") || node is not JsonObject field) continue;

            string fieldType = field["type"]?.GetValue<string>() ?? "";
            bool isRequired = field["required"]?.GetValue<bool>() == true;
            if (!isRequired) continue;

            string name = field["name"]?.GetValue<string>() ?? key;

            if (fieldType == "CustomOption")
            {
                var allowedValues = new List<CustomFieldOption>();
                if (field["_embedded"]?["allowedValues"] is JsonArray values)
                {
                    foreach (var value in values)
                    {
                        if (value is null) continue;
                        int id = value["id"]?.GetValue<int>() ?? 0;
                        string optionValue = value["value"]?.GetValue<string>() ?? "";
                        allowedValues.Add(new CustomFieldOption(id, optionValue));
                    }
                }

                // Saltar campos de lista sin opciones configuradas; no se puede preguntar al usuario.
                if (allowedValues.Count == 0) continue;

                result.Add(new RequiredCustomField(key, name, fieldType, allowedValues));
            }
            else if (textFieldTypes.Contains(fieldType))
            {
                result.Add(new RequiredCustomField(key, name, fieldType, new List<CustomFieldOption>()));
            }
        }

        return result;
    }

    /// <summary>
    /// Resuelve un tipo de work package válido para el proyecto cuando no se especifica uno.
    /// No se puede asumir un ID fijo (ej. 1 = "Task") porque los tipos disponibles varían por proyecto;
    /// usar un ID inexistente hace que OpenProject ignore silenciosamente los campos personalizados enviados.
    /// </summary>
    private async Task<int> GetDefaultTypeIdAsync(int projectId)
    {
        string url = $"/api/v3/projects/{projectId}/types";
        var response = await _client.GetAsync(url);
        if (!response.IsSuccessStatusCode) return 1;

        var json = await response.Content.ReadAsStringAsync();
        var elements = JsonNode.Parse(json)?["_embedded"]?["elements"]?.AsArray();
        if (elements is null || elements.Count == 0) return 1;

        var taskType = elements.FirstOrDefault(e =>
        {
            var name = e?["name"]?.GetValue<string>() ?? "";
            return name.Contains("Task", StringComparison.OrdinalIgnoreCase) || name.Contains("Tarea", StringComparison.OrdinalIgnoreCase);
        });

        return (taskType ?? elements[0])?["id"]?.GetValue<int>() ?? 1;
    }
}
