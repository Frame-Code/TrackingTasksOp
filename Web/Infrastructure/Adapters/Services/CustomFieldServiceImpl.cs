using System.Net;
using System.Text.Json;
using Application.Ports.Services;
using Domain.Entities.OpenProjectEntities;
using Microsoft.Extensions.Options;
using Web.Infrastructure.Config.Settings;

namespace Web.Infrastructure.Adapters.Services;

public class CustomFieldServiceImpl(
    IHttpClientFactory httpClientFactory,
    ILogger<CustomFieldServiceImpl> logger,
    IOptions<OpenProjectSettings> settings
) : ICustomFieldService
{
    private readonly OpenProjectSettings _settings = settings.Value;
    private readonly HttpClient _client = httpClientFactory.CreateClient(settings.Value.HttpClientName);

    // IDs de los campos personalizados según el error del usuario: 
    // Area = customField3, Modulo = customField5
    // En OpenProject, las opciones de campos de selección se obtienen vía /api/v3/custom_fields/{id}
    // o directamente de las opciones si conocemos el ID del campo.
    private const int AREA_FIELD_ID = 3;
    private const int MODULE_FIELD_ID = 5;

    public async Task<List<CustomOption>> ListAreas() => await GetOptions(AREA_FIELD_ID);
    public async Task<List<CustomOption>> ListModules() => await GetOptions(MODULE_FIELD_ID);

    public async Task<CustomOption?> FindAreaByName(string name)
    {
        var list = await ListAreas();
        return list.FirstOrDefault(o => o.Value.Contains(name, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<CustomOption?> FindModuleByName(string name)
    {
        var list = await ListModules();
        return list.FirstOrDefault(o => o.Value.Contains(name, StringComparison.OrdinalIgnoreCase));
    }

    private async Task<List<CustomOption>> GetOptions(int fieldId)
    {
        string url = $"{_settings.BaseUrl.TrimEnd('/')}/api/v3/custom_fields/{fieldId}/custom_options";
        logger.LogInformation("Fetching custom options for field {Id} from: {Url}", fieldId, url);

        try
        {
            var response = await _client.GetAsync(url);
            if (!response.IsSuccessStatusCode) return new List<CustomOption>();

            var json = await response.Content.ReadAsStringAsync();
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var collection = JsonSerializer.Deserialize<CustomOptionCollection>(json, options);

            return collection?.Embedded?.Elements ?? new List<CustomOption>();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error fetching custom options for field {Id}", fieldId);
            return new List<CustomOption>();
        }
    }
}
