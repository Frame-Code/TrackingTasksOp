using System.Text.Json.Serialization;

namespace Domain.Entities.OpenProjectEntities;

public class CollectionBase
{
    [JsonPropertyName("_type")] 
    public string Type { get; set; } = string.Empty;
}