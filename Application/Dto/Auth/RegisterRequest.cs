namespace Application.Dto.Auth;

public abstract class RegisterRequest
{
    public string OpenProjectInstanceUrl { get; init; } = null!;
}