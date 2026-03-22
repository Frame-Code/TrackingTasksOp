namespace Domain.Entities.TrackingTasksEntities;

public class Project
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public string Identifier { get; set; } = null!;
    public bool IsActive { get; set; }
}