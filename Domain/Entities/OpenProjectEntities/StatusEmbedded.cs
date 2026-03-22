using System.Text.Json.Serialization;

namespace Domain.Entities.OpenProjectEntities;

public class StatusEmbedded
{
    [JsonPropertyName("elements")] 
    public List<Status> Elements { get; set; } = new List<Status>();
}