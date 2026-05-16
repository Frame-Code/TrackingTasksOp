namespace Domain.Entities.TrackingTasksEntities;

public class Task
{
    public int WorkPackageId { get; set; }
    public string UserId { get; set; } = null!;
    public string  Name { get; set; } = null!;
    public string? Description { get; set; } = null!;
    public DateTime? CreatedAt { get; init; } = DateTime.Now;
    public int OpenProjectInstanceId { get; set; }
    public OpenProjectInstance OpenProjectInstance { get; set; } = null!;
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