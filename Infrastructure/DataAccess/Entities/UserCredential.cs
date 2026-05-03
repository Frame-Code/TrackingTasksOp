using Infrastructure.DataAccess.Entities.Enums;

namespace Infrastructure.DataAccess.Entities;

public class UserCredential
{
    public string UserId { get; set; } = null!;
    public ApplicationUser User { get; set; } = null!;
    public string? EncryptedApiKey { get; set; }
    public ApiKeyStatus ApiKeyStatus { get; set; }
    public DateTime ApiKeyLastValidatedAt { get; set; }
    public string? EncryptedOAuthAccessToken { get; set; }
    public string? EncryptedOAuthRefreshToken { get; set; }
    public DateTime OAuthTokenExpiresAt { get; set; }
    public string? OAuthScope { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}