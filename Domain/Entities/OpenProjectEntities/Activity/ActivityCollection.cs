using System.Text.Json.Serialization;

namespace Domain.Entities.OpenProjectEntities.Activity;

public class ActivityCollection : CollectionBase
{
    [JsonPropertyName("_embedded")]
    public ActivityEmbedded Embedded { get; set; } = null!;
}