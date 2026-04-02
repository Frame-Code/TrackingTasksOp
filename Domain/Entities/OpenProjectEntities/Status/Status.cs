using System.Text.Json.Serialization;

namespace Domain.Entities.OpenProjectEntities.Status;

public class Status
{
    [JsonPropertyName("_type")] 
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")] 
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("isClosed")]
    public bool IsClosed { get; set; }
}