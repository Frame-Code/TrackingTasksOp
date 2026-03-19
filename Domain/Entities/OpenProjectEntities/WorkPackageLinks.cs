using System.Text.Json.Serialization;

namespace Domain.Entities.OpenProjectEntities;

//Representa los campos adicionales de cada tarea
public class WorkPackageLinks
{
    [JsonPropertyName("status")]
    public LinkObject Status { get; set; } = new LinkObject();
    
    [JsonPropertyName("type")]
    public LinkObject Type { get; set; } = new LinkObject();
    
    [JsonPropertyName("priority")]
    public LinkObject Priority { get; set; } = new LinkObject();
    
    [JsonPropertyName("assignee")]
    public LinkObject Assignee { get; set; } = new LinkObject();
    
    [JsonPropertyName("project")]
    public LinkObject Project { get; set; } = new LinkObject();
}