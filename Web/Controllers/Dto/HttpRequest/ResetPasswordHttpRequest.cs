namespace Web.Controllers.Dto.HttpRequest;

public class ResetPasswordHttpRequest
{
    public string Email { get; init; } = null!;
    public string Code { get; init; } = null!;
    public string NewPassword { get; init; } = null!;
}
