using System.Text.Json.Serialization;

namespace Domain.Entities.OpenProjectEntities.WorkPackage;

public class Attachment
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("fileName")]
    public string FileName { get; set; } = string.Empty;

    [JsonPropertyName("contentType")]
    public string ContentType { get; set; } = string.Empty;

    [JsonPropertyName("fileSize")]
    public int FileSize { get; set; }

    [JsonPropertyName("_links")]
    public AttachmentLinks Links { get; set; } = new();
}

public class AttachmentLinks
{
    [JsonPropertyName("self")]
    public LinkObject Self { get; set; } = new();

    [JsonPropertyName("downloadLocation")]
    public LinkObject DownloadLocation { get; set; } = new();
}

public class AttachmentCollection : CollectionBase
{
    [JsonPropertyName("_embedded")]
    public AttachmentEmbedded Embedded { get; set; } = new();
}

public class AttachmentEmbedded
{
    [JsonPropertyName("elements")]
    public List<Attachment> Elements { get; set; } = new();
}
