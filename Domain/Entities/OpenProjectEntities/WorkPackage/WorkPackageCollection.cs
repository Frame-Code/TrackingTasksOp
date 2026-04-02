using System.Text.Json.Serialization;

namespace Domain.Entities.OpenProjectEntities.WorkPackage;

//Representa un sobre paginado de la respuesta, siendo la raíz del json
public class WorkPackageCollection : CollectionBase
{
    [JsonPropertyName("total")]
    public int Total { get; set; }
    
    [JsonPropertyName("count")]
    public int Count { get; set; }
    
    [JsonPropertyName("pageSize")]
    public int PageSize { get; set; }
    
    [JsonPropertyName("offset")]
    public int Offset { get; set; }
    
    [JsonPropertyName("_embedded")]
    public EmbeddedWokPackage Embedded  { get; set; } = new EmbeddedWokPackage();
}