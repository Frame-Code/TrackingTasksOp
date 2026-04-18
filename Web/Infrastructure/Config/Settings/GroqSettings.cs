namespace Web.Infrastructure.Config.Settings
{
    public class GroqSettings
    {
        public string ApiKey { get; set; } = "";
        public string Model { get; set; } = "llama-3.3-70b-versatile";
        public float Temperature { get; set; } = 0.1f;
    }
}
