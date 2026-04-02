using System.Text.Json.Serialization;

namespace Domain.Entities.OpenProjectEntities.Status;

public class StatusEmbedded
{
    [JsonPropertyName("elements")] 
    public List<Status> Elements { get; set; } = new List<Status>();
}