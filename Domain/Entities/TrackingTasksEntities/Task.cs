namespace Domain.Entities.TrackingTasksEntities;

public class Task
{
    public int WorkPackageId { get; set; }
    public string  Name { get; set; } = null!;
    public string? Description { get; set; } = null!;
    public DateTime? CreatedAt { get; init; } = DateTime.Now;
    public int ProjectId { get; set; }
    public Project Project { get; set; } = null!;
    public int StatusTaskId { get; set; }
    public StatusTask StatusTask { get; set; } = null!;
    public IEnumerable<TaskTimeDetail> TasksTimeDetails { get; set; } = new List<TaskTimeDetail>();

    public double GetTotalHoursWorked()
    {
        return TasksTimeDetails
            .Where(task => task.GetHoursWorked().HasValue)
            .GroupBy(x => x.GetHoursWorked())
            .Select(x => x.Sum(t => t?.GetHoursWorked()?.TotalHours))
            .Sum(x => x ?? 0);
    }
}