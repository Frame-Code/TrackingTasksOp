namespace Domain.Entities.TrackingTasksEntities;

public class StatusTask
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public bool IsClosed { get; set; }
}