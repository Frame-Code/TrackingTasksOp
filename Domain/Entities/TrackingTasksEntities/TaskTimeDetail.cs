namespace Domain.Entities.TrackingTasksEntities;

public class TaskTimeDetail
{
    public int Id { get; set; }
    public DateTime StartTime { get; init; } = DateTime.Now;
    public DateTime? EndTime { get; set; }
    public int IdTask  { get; set; }
    public Task Task { get; set; } = null!;

    public TimeSpan? GetHoursWorked() => 
        EndTime - StartTime; 
}