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

    /// <summary>
    /// La sesion abierta, si hay alguna. NO es "la de StartTime mas grande": una entrada
    /// manual con hora posterior hacia que esa consulta devolviera una sesion CERRADA, y el
    /// sistema concluia que no habia ninguna activa. Sintomas reales: pausar tiraba "doesn`t
    /// have an active session", y arrancar apilaba sesiones abiertas en vez de cerrar la
    /// anterior. Si por datos viejos hay varias abiertas, devuelve la mas reciente.
    /// </summary>
    public TaskTimeDetail? GetActiveSession() =>
        TasksTimeDetails
            .Where(d => d.EndTime == null)
            .OrderBy(d => d.StartTime)
            .LastOrDefault();

    public double GetTotalHoursWorked()
    {
        return TasksTimeDetails
            .Where(task => task.GetHoursWorked().HasValue)
            .GroupBy(x => x.GetHoursWorked())
            .Select(x => x.Sum(t => t?.GetHoursWorked()?.TotalHours))
            .Sum(x => x ?? 0);
    }
}