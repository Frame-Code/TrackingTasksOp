using System.Text.Json.Serialization;

namespace Domain.Entities.OpenProjectEntities.WorkPackage
{
    //Representa todo tipo de tarea dentro de OpenProject
    public class WorkPackage
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }
        
        [JsonPropertyName("subject")]
        public string Subject { get; set; } = string.Empty;
        
        [JsonPropertyName("createdAt")]
        public DateTime? CreatedAt { get; set; }
        
        [JsonPropertyName("updatedAt")]
        public DateTime? UpdatedAt { get; set; }
        
        [JsonPropertyName("startDate")]
        public string StartDate { get; set; } = string.Empty;
        
        [JsonPropertyName("dueDate")]
        public string DueDate { get; set; } = string.Empty;
        
        [JsonPropertyName("percentageDone")]
        public int? PercentageDone { get; set; }
        
        [JsonPropertyName("description")]
        public FormattableField Description { get; set; } = new FormattableField();
        
        [JsonPropertyName("_links")]
        public WorkPackageLinks Links { get; set; } = new WorkPackageLinks();
    }
}
