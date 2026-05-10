namespace Domain.Entities.TrackingTasksEntities;

public class OpenProjectInstance
{
    public int Id { get; set; }
    public string BaseUrl { get; set; } = null!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}