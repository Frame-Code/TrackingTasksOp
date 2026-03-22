namespace Application.Dto.Tasks;

public class StarTaskRequest
{
    public int OpenProjectId { get; init; }
    public string Name { get; init; } = null!;
    public string? Description { get; init; }
    public int ProjectId { get; init; }
    public int StatusId { get; init; }
}