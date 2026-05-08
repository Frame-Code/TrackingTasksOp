namespace Application.Dto.Auth;

public class LocalRegisterRequest : RegisterRequest
{
    public string Email { get; init; } = null!;
    public string Password { get; init; } = null!;
    public string ConfirmPassword { get; init; } = null!;
    public string ApiKey { get; init; } = null!;
}