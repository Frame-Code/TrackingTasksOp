namespace Infrastructure.DataAccess.Entities;

public class OAuthCredential : UserCredential
{
    public string EncryptedOAuthAccessToken { get; set; } = null!;
    public string? EncryptedOAuthRefreshToken { get; set; }
    public DateTime OAuthTokenExpiresAt { get; set; }
    public string OAuthScope { get; set; } = null!;
}