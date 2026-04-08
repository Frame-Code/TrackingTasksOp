namespace Web.Infrastructure.Config.Settings;

/// <summary>
/// Contiene la configuración para el servicio de Ollama local.
/// </summary>
public class OllamaSettings
{
    public string BaseUrl { get; set; } = "http://localhost:11434";
    public string Model { get; set; } = "phi3:mini";
    public float Temperature { get; set; } = 0.1f;
}
