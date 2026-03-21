namespace Application.Dto.Tasks;

public class StarTaskRequest
{
    public int OpenProjectId { get; set; }
    public string Name { get; init; } = null!;
    public string? Description { get; set; }
    public int ProjectId { get; set; }
}