namespace Web.Infrastructure.Config.Settings;
public class GroqSettings
{
    public string ApiKey { get; set; } = null!;
    public string Model { get; set; } = null!;
    public float Temperature { get; set; } 
    public string HttpClientName { get; set; } = null!; 
    public string BaseUrl {get; set;} = null!;
}

