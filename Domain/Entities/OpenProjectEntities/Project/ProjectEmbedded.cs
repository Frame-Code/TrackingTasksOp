using System.Text.Json.Serialization;

namespace Domain.Entities.OpenProjectEntities.Project;

public class ProjectEmbedded
{
    [JsonPropertyName("elements")]
    public List<Project> Projects { get; set; } = new List<Project>();
}