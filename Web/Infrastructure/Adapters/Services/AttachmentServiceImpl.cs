using System.Net;
using System.Text.Json;
using Application.Ports.Services;
using Domain.Entities.OpenProjectEntities.WorkPackage;
using Microsoft.Extensions.Options;
using Web.Infrastructure.Config.Settings;

namespace Web.Infrastructure.Adapters.Services;

public class AttachmentServiceImpl(
    IHttpClientFactory httpClientFactory,
    ILogger<AttachmentServiceImpl> logger,
    IOptions<OpenProjectSettings> settings
) : IAttachmentService
{
    private readonly OpenProjectSettings _settings = settings.Value;
    private readonly HttpClient _client = httpClientFactory.CreateClient(settings.Value.HttpClientName);

    public async Task<List<Attachment>> GetAttachmentsAsync(int workPackageId)
    {
        string baseUrl = _settings.BaseUrl.TrimEnd('/');
        string url = $"{baseUrl}/api/v3/work_packages/{workPackageId}/attachments";
        
        logger.LogInformation("Fetching attachments list for WP #{Id} from: {Url}", workPackageId, url);

        try
        {
            var response = await _client.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                string error = await response.Content.ReadAsStringAsync();
                logger.LogError("Error fetching attachments list (HTTP {Status}): {Error}", response.StatusCode, error);
                return new List<Attachment>();
            }

            var json = await response.Content.ReadAsStringAsync();
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var collection = JsonSerializer.Deserialize<AttachmentCollection>(json, options);

            return collection?.Embedded?.Elements ?? new List<Attachment>();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Exception while fetching attachments for WP #{Id}", workPackageId);
            return new List<Attachment>();
        }
    }

    public async Task<(byte[] Content, string ContentType, string FileName)> GetAttachmentContentAsync(int attachmentId)
    {
        string baseUrl = _settings.BaseUrl.TrimEnd('/');
        
        // 1. Obtener metadatos
        string metaUrl = $"{baseUrl}/api/v3/attachments/{attachmentId}";
        logger.LogInformation("Fetching attachment metadata for #{Id} from: {Url}", attachmentId, metaUrl);

        var metaResponse = await _client.GetAsync(metaUrl);
        if (!metaResponse.IsSuccessStatusCode)
        {
            var err = await metaResponse.Content.ReadAsStringAsync();
            logger.LogError("Attachment metadata not found for #{Id}. Status: {Status}, Error: {Error}", attachmentId, metaResponse.StatusCode, err);
            throw new Exception($"No se encontró el adjunto #{attachmentId} en OpenProject (HTTP {metaResponse.StatusCode}).");
        }

        var metaJson = await metaResponse.Content.ReadAsStringAsync();
        var attachment = JsonSerializer.Deserialize<Attachment>(metaJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (attachment?.Links?.DownloadLocation?.Href == null) 
        {
            logger.LogError("Attachment #{Id} metadata does not contain a downloadLocation.", attachmentId);
            throw new Exception("El adjunto no tiene una ubicación de descarga válida definida en OpenProject.");
        }

        // 2. Determinar la URL de descarga
        string href = attachment.Links.DownloadLocation.Href;
        string downloadUrl = href.StartsWith("http", StringComparison.OrdinalIgnoreCase) 
            ? href 
            : $"{baseUrl}{href}";

        logger.LogInformation("Downloading attachment content for #{Id} from: {Url}", attachmentId, downloadUrl);

        // 3. Descargar el contenido real
        var contentResponse = await _client.GetAsync(downloadUrl);
        if (!contentResponse.IsSuccessStatusCode)
        {
            var err = await contentResponse.Content.ReadAsStringAsync();
            logger.LogError("Failed to download content for attachment #{Id} from {Url}. Status: {Status}, Error: {Error}", 
                attachmentId, downloadUrl, contentResponse.StatusCode, err);
            throw new Exception($"Error al descargar el contenido del adjunto desde OpenProject (HTTP {contentResponse.StatusCode}).");
        }

        var content = await contentResponse.Content.ReadAsByteArrayAsync();
        return (content, attachment.ContentType, attachment.FileName);
    }
}
