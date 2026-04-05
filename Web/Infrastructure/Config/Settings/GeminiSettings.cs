namespace Web.Infrastructure.Config.Settings;

/// <summary>
/// Contiene la configuración para el API de Gemini en Vertex AI.
/// </summary>
public class GeminiSettings
{
    public required string ProjectId { get; set; }
    public required string Location { get; set; }
    public required string Publisher { get; set; }
    public required string Model { get; set; }
}
