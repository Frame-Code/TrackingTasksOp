using System.Text.Json.Serialization;

namespace Domain.Entities.OpenProjectEntities;

public class CustomOption
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("value")]
    public string Value { get; set; } = string.Empty;
}

public class CustomOptionCollection : CollectionBase
{
    [JsonPropertyName("_embedded")]
    public CustomOptionEmbedded Embedded { get; set; } = new();
}

public class CustomOptionEmbedded
{
    [JsonPropertyName("elements")]
    public List<CustomOption> Elements { get; set; } = new();
}
