using System.Text.Json.Serialization;
using System.Xml;

namespace Domain.Entities.OpenProjectEntities.TimeEntries;

public class TimeEntryLinks
{
    [JsonPropertyName("workPackage")] public LinkObject WorkPackage { get; set; } = new();
    [JsonPropertyName("project")] public LinkObject Project { get; set; } = new();
    [JsonPropertyName("activity")] public LinkObject Activity { get; set; } = new();
    [JsonPropertyName("user")] public LinkObject User { get; set; } = new();
}

public class OpTimeEntry
{
    [JsonPropertyName("id")] public int Id { get; set; }

    /// <summary>Fecha en que se trabajó, formato yyyy-MM-dd.</summary>
    [JsonPropertyName("spentOn")] public string SpentOn { get; set; } = string.Empty;

    /// <summary>Duración en ISO 8601, ej. "PT1H30M".</summary>
    [JsonPropertyName("hours")] public string Hours { get; set; } = string.Empty;

    [JsonPropertyName("_links")] public TimeEntryLinks Links { get; set; } = new();

    public DateOnly SpentOnDate =>
        DateOnly.TryParse(SpentOn, out var d) ? d : DateOnly.MinValue;

    /// <summary>
    /// Convierte la duración ISO 8601 de OpenProject a horas decimales.
    /// XmlConvert.ToTimeSpan cubre el formato PnDTnHnMnS que devuelve la API.
    /// </summary>
    public double HoursAsDouble
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Hours)) return 0;
            try { return XmlConvert.ToTimeSpan(Hours).TotalHours; }
            catch (FormatException) { return 0; }
        }
    }

    public int WorkPackageId =>
        int.TryParse(Links.WorkPackage.Href?.Split('/').LastOrDefault(), out var id) ? id : 0;

    public string WorkPackageTitle => Links.WorkPackage.Title;
    public string ProjectTitle => Links.Project.Title;
    public string ActivityTitle => Links.Activity.Title;
}
