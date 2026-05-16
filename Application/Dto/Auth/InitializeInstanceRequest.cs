namespace Application.Dto.Auth;

public class InitializeInstanceRequest
{
    public string UserId { get; init; } = null!;
    public string Username { get; init; } = null!;
    public int OpenProjectInstanceId { get; init; }
    public string OpenProjectInstanceUrl { get; init; } = null!;
    public string ApiKey { get; init; } = null!;
}