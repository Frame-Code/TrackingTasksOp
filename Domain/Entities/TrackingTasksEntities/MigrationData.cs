namespace Domain.Entities.TrackingTasksEntities;

public class MigrationData
{
    public int Id { get; set; }
    public string UserId { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string Description { get; set; } = null!;
    public int OpenProjectInstanceId { get; set; }
    public OpenProjectInstance OpenProjectInstance { get; set; } = null!;
    public DateTime CreatedAt { get; init; } = DateTime.Now;
}