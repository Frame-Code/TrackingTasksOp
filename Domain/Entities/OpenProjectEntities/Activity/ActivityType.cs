using System.Text.Json.Serialization;

namespace Domain.Entities.OpenProjectEntities.Activity;

public class ActivityType
{
<<<<<<< HEAD
    [JsonPropertyName("_embedded")]
    public ActivityTypeEmbedded Embedded { get; set; } = new ActivityTypeEmbedded();
}

public class ActivityTypeEmbedded
{
    [JsonPropertyName("allowedValues")]
    public List<ActivityAllowedValue> AllowedValues { get; set; } = new List<ActivityAllowedValue>();
}
=======
    [JsonPropertyName("_embedded")] 
    public ActivityTypeEmbedded Embedded { get; set; }= new ActivityTypeEmbedded();
}
>>>>>>> 43a2f6a7702ea35cec6982dd843371d7f6bf2b9b
