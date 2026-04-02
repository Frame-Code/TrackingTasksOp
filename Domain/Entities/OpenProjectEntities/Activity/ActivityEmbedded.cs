using System.Text.Json.Serialization;

namespace Domain.Entities.OpenProjectEntities.Activity;

public class ActivityEmbedded
{
    [JsonPropertyName("schema")]
    public ActivitySchema Schema { get; set; } = null!;
}