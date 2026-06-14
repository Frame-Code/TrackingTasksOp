namespace Application.Dto.Tasks;

public class StarTaskRequest
{
    public int WorkPackageId { get; init; }
    public int? ActivityId { get; init; }
    public string? Comment { get; init; }
    public string Name { get; init; } = null!;
    public string? Description { get; init; }
    public int ProjectId { get; init; }
    public int StatusId { get; init; }
    public int? AssigneeId { get; init; }
    public int? ResponsibleId { get; init; }
    public DateOnly? StartDate { get; init; }
    public DateOnly? DueDate { get; init; }
    public Dictionary<string, int>? CustomFieldOptionIds { get; init; }
}
