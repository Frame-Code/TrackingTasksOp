using System.Text.Json.Serialization;

namespace Domain.Entities.OpenProjectEntities.Activity;

public class ActivitySchema
{
    [JsonPropertyName("activity|")]
    public ActivityType Type { get; set; } = null!;
}