namespace Web.Controllers.Dto.HttpRequest;

public class LocalLoginHttpRequest
{
    public string Email { get; init; } = null!;
    public string Password { get; init; } = null!;
}