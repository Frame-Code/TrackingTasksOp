using System.Text.Json.Serialization;

namespace Domain.Entities.OpenProjectEntities;

//Representa cada objeto único de las relaciones de una tarea
public class LinkObject
{
    [JsonPropertyName("href")]
    public string Href { get; set; } = string.Empty;
    
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// El ID que va al final del href ("/api/v3/work_packages/412" → 412). 0 si el link
    /// viene vacío, que es como OpenProject expresa "sin padre", "sin asignado", etc.
    /// </summary>
    [JsonIgnore]
    public int Id => int.TryParse(Href?.TrimEnd('/').Split('/').LastOrDefault(), out var id) ? id : 0;
}