using System.Text.Json.Serialization;

namespace Domain.Entities.OpenProjectEntities.OAuth;

public class Token
{
    [JsonPropertyName("access_token")]
    public string AccessToken { get; set; } = null!;
    
    [JsonPropertyName("token_type")]
    public string TokenType { get; set; } = null!;
    
    [JsonPropertyName("expires_in")]
    public decimal ExpiresIn { get; set; }
    
    [JsonPropertyName("refresh_token")]
    public string RefreshToken { get; set; } = null!;
    
    [JsonPropertyName("scope")]
    public string Scope { get; set; } = null!;
    
    [JsonPropertyName("created_at")]
    public string CreatedAt { get; set; } = null!;
}