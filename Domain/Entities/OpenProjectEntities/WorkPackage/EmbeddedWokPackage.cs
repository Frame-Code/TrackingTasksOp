using System.Text.Json.Serialization;

namespace Domain.Entities.OpenProjectEntities.WorkPackage;

//Representa los recursos anidados de la respuesta
public class EmbeddedWokPackage
{
    [JsonPropertyName("elements")]
    public List<WorkPackage> Elements { get; set; } = new List<WorkPackage>();
}