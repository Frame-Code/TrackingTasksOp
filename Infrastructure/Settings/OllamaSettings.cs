namespace Infrastructure.Settings;

/// <summary>
/// Contiene la configuración para el servicio de Ollama local.
/// </summary>
public class OllamaSettings
{
    public string BaseUrl { get; set; } = null!;
    public string Model { get; set; } = null!;
    public float Temperature { get; set; } 
}
