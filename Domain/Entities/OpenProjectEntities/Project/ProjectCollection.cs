using System.Text.Json.Serialization;

namespace Domain.Entities.OpenProjectEntities.Project;

public class ProjectCollection
{
    [JsonPropertyName("_type")] 
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("total")]
    public int Total { get; set; }

    [JsonPropertyName("count")]
    public int Count { get; set; }

    [JsonPropertyName("_embedded")] 
    public ProjectEmbedded Embedded { get; set; } = new ProjectEmbedded();
}