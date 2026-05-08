namespace Application.Dto.Auth;

public class AuthenticatedUserResponse
{
    public string UserId { get; init; } = null!;
    public string Email { get; init; } = null!;
    public string Name { get; init; } = null!;
    public string OpenProjectInstanceUrl { get; init; } = null!;
    public int OpenProjectUserId { get; init; }
}
