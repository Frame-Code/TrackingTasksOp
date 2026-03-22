namespace Domain.Entities.TrackingTasksEntities;

public class MigrationData
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public string Description { get; set; } = null!;
    public DateTime CreatedAt { get; init; } = DateTime.Now;
}