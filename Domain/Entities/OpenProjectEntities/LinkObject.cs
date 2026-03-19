using System.Text.Json.Serialization;

namespace Domain.Entities.OpenProjectEntities;

//Representa cada objeto único de las relaciones de una tarea
public class LinkObject
{
    [JsonPropertyName("href")]
    public string Href { get; set; } = string.Empty;
    
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;
}