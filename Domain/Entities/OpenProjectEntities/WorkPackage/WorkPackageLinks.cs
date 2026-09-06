using System.Text.Json.Serialization;

namespace Domain.Entities.OpenProjectEntities.WorkPackage;

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

    /// <summary>Responsable ("accountable" en la UI de OpenProject). Distinto del asignado.</summary>
    [JsonPropertyName("responsible")]
    public LinkObject Responsible { get; set; } = new LinkObject();
    
    [JsonPropertyName("project")]
    public LinkObject Project { get; set; } = new LinkObject();

    /// <summary>Padre directo. Href vacío = la tarea es raíz.</summary>
    [JsonPropertyName("parent")]
    public LinkObject Parent { get; set; } = new LinkObject();

    /// <summary>
    /// Hijos directos, cuando OpenProject los incluye. El árbol no los usa para pintar
    /// (los pide por su endpoint al expandir), solo para saber si el nodo tiene algo debajo.
    /// </summary>
    [JsonPropertyName("children")]
    public List<LinkObject> Children { get; set; } = [];

    /// <summary>
    /// Cadena de ancestros, de la raíz al padre. Cada link ya trae el título, así que la
    /// miga de pan de la tarjeta no cuesta ninguna llamada extra.
    /// </summary>
    [JsonPropertyName("ancestors")]
    public List<LinkObject> Ancestors { get; set; } = [];
}