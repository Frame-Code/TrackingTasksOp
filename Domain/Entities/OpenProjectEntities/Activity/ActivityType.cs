using System.Text.Json.Serialization;

namespace Domain.Entities.OpenProjectEntities.Activity;

public class ActivityType
{
    [JsonPropertyName("_embedded")]
    public ActivityTypeEmbedded Embedded { get; set; } = new ActivityTypeEmbedded();
}

public class ActivityTypeEmbedded
{
    [JsonPropertyName("allowedValues")]
    public List<ActivityAllowedValue> AllowedValues { get; set; } = new List<ActivityAllowedValue>();
}
