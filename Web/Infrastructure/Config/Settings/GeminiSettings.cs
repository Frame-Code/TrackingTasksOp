namespace Web.Infrastructure.Config.Settings;

/// <summary>
/// Contiene la configuración para el API de Gemini en Vertex AI.
/// </summary>
public class GeminiSettings
{
    public string ProjectId { get; set; } = null!;
    public string Location { get; set; } = null!;
    public string Publisher { get; set; } = null!;
    public string Model { get; set; } = null!;
    public string ApiKey { get; set; } = null!;
}
