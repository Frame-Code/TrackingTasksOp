using System.Text.Json.Serialization;

namespace Domain.Entities.OpenProjectEntities.TimeEntries;

public class TimeEntryEmbedded
{
    [JsonPropertyName("elements")] public List<OpTimeEntry> Elements { get; set; } = [];
}

public class TimeEntryCollection : CollectionBase
{
    [JsonPropertyName("total")] public int Total { get; set; }
    [JsonPropertyName("count")] public int Count { get; set; }
    [JsonPropertyName("pageSize")] public int PageSize { get; set; }
    [JsonPropertyName("offset")] public int Offset { get; set; }
    [JsonPropertyName("_embedded")] public TimeEntryEmbedded? Embedded { get; set; }
}
