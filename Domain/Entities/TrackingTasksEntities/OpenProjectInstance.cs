namespace Domain.Entities.TrackingTasksEntities;

public class OpenProjectInstance
{
    public int Id { get; set; }
    public string BaseUrl { get; set; } = null!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? OAuthClientId { get; set; }
    public string? EncryptedOAuthClientSecret { get; set; }
    public string? Alias { get; set; }
    public DateTime? OAuthConnectedAt { get; set; }
}