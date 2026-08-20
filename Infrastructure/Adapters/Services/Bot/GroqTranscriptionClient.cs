using System.Net.Http.Headers;
using System.Text.Json;
using Application.Ports.Services;
using Infrastructure.Settings;
using Microsoft.Extensions.Options;

namespace Infrastructure.Adapters.Services.Bot;

/// <summary>
/// Adapter que transcribe audio a texto usando el endpoint de transcripciones de Groq
/// (modelo Whisper), reutilizando el mismo HttpClient/credenciales configurados para Groq.
/// </summary>
public class GroqTranscriptionClient(
    IHttpClientFactory httpClientFactory,
    IOptions<GroqSettings> groqSettings,
    GroqAuthHeaderProvider authHeaderProvider) : IAudioTranscriptionService
{
    private readonly GroqSettings _groqSettings = groqSettings.Value;
    private HttpClient? _httpClient;

    private HttpClient HttpClient => _httpClient ??= httpClientFactory.CreateClient(KeyedServicesNames.GroqHttpClientName);

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_groqSettings.ApiKey);

    public async Task<string> TranscribeAsync(Stream audio, string fileName, string contentType, CancellationToken ct = default)
    {
        using var content = new MultipartFormDataContent();

        var fileContent = new StreamContent(audio);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(
            string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType);
        content.Add(fileContent, "file", string.IsNullOrWhiteSpace(fileName) ? "audio.webm" : fileName);

        content.Add(new StringContent(_groqSettings.TranscriptionModel), "model");
        content.Add(new StringContent("json"), "response_format");
        if (!string.IsNullOrWhiteSpace(_groqSettings.TranscriptionLanguage))
            content.Add(new StringContent(_groqSettings.TranscriptionLanguage), "language");

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, _groqSettings.TranscriptionBaseUrl) { Content = content };
        httpRequest.Headers.Authorization = await authHeaderProvider.GetAuthorizationHeaderAsync();
        var httpResponse = await HttpClient.SendAsync(httpRequest, ct);

        if (!httpResponse.IsSuccessStatusCode)
        {
            var errorBody = await httpResponse.Content.ReadAsStringAsync(ct);
            throw new Exception($"Groq Transcription API error ({httpResponse.StatusCode}): {errorBody}");
        }

        var responseJson = await httpResponse.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(responseJson);
        return doc.RootElement.TryGetProperty("text", out var textProp) ? (textProp.GetString() ?? "").Trim() : "";
    }
}
