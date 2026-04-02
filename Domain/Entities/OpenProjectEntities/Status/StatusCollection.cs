using System.Text.Json.Serialization;

namespace Domain.Entities.OpenProjectEntities.Status;

public class StatusCollection : CollectionBase
{
    [JsonPropertyName("total")]
    public int Total { get; set; }

    [JsonPropertyName("count")]
    public int Count { get; set; }

    [JsonPropertyName("_embedded")] 
    public StatusEmbedded Embedded { get; set; } = new StatusEmbedded();
}