namespace Application.Dto.Auth;

public class LocalLoginRequest
{
    public string Email { get; init; } = null!;
    public string Password { get; init; } = null!;
}