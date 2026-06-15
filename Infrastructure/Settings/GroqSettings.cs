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
}

