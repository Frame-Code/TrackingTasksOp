namespace Infrastructure.Settings;
public class GroqSettings
{
    public string ApiKey { get; set; } = null!;
    public string Model { get; set; } = null!;
    public float Temperature { get; set; }
    public string BaseUrl {get; set;} = null!;
    public string TranscriptionModel { get; set; } = "whisper-large-v3-turbo";
    public string TranscriptionBaseUrl { get; set; } = "https://api.groq.com/openai/v1/audio/transcriptions";
    public string TranscriptionLanguage { get; set; } = "es";

    /// <summary>
    /// Tope diario de mensajes al bot por usuario que use la key COMPARTIDA (no aplica a
    /// quien configuró su propia key de Groq). 0 o negativo desactiva el límite.
    /// </summary>
    public int DailyMessageLimitPerUser { get; set; } = 50;
}

