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
}