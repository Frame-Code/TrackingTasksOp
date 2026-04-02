using System.Text.Json.Serialization;

namespace Domain.Entities.OpenProjectEntities.Project;

public class Project
{
    [JsonPropertyName("_type")] 
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("id")]
    public int Id { get; set; }
    
    [JsonPropertyName("identifier")] 
    public string Identifier { get; set; } = string.Empty;

    [JsonPropertyName("name")] 
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("active")]
    public bool IsActive { get; set; }
    
}