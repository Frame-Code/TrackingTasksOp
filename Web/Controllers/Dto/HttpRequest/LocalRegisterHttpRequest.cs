namespace Web.Controllers.Dto.HttpRequest;

public class LocalRegisterHttpRequest
{
    public string Email { get; init; } = null!;
    public string Password { get; init; } = null!;
    public string ApiKey { get; init; } = null!;
    public string OpenProjectInstanceUrl { get; init; } = null!;
    public bool ValidateSemanticOpenProjectUrl { get; init; } = true;
}