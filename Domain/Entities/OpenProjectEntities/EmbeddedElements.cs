using System.Text.Json.Serialization;

namespace Domain.Entities.OpenProjectEntities;

//Representa los recursos anidados de la respuesta
public class EmbeddedElements
{
    [JsonPropertyName("elements")]
    public List<WorkPackage> Elements { get; set; } = new List<WorkPackage>();
}