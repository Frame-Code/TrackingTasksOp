using System.Text.Json.Serialization;

namespace Domain.Entities.OpenProjectEntities.Activity;

public class ActivityTypeEmbedded
{
    [JsonPropertyName("allowedValues")]
    public List<ActivityAllowedValue>  AllowedValues { get; set; } = new List<ActivityAllowedValue>();
}