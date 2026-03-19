using System.Text.Json.Serialization;

namespace Domain.Entities.OpenProjectEntities;

//Representa la descripción de un WorkPackage
public class FormattableField
{
    [JsonPropertyName("raw")]
    public string Raw { get; set; } = string.Empty;
}

