namespace Infrastructure.Adapters.Services.Bot;

public class GroqAction
{
    public string Action { get; set; } = "";
    public Dictionary<string, object>? Params { get; set; }
}
