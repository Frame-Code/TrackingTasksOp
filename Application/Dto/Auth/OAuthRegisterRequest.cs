namespace Application.Dto.Auth;

public class OAuthRegisterRequest : RegisterRequest
{
    public string AccessToken { get; init; } = null!;
    public string? RefreshToken { get; init; }
    public DateTime ExpiresAt { get; init; }
    public string OAuthScope { get; init; } = null!;
}