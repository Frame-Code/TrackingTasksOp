using System.Text.Json.Serialization;

namespace Domain.Entities.OpenProjectEntities.OAuth;

public class Token
{
    [JsonPropertyName("access_token")]
    public string AccessToken { get; set; } = null!;
    
    [JsonPropertyName("token_type")]
    public string TokenType { get; set; } = null!;
    
    // Doorkeeper manda expires_in/created_at como números JSON (segundos y epoch Unix),
    // no como strings; AllowReadingFromString los tolera igual si algún proxy los stringifica.
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    [JsonPropertyName("expires_in")]
    public decimal ExpiresIn { get; set; }

    [JsonPropertyName("refresh_token")]
    public string RefreshToken { get; set; } = null!;

    [JsonPropertyName("scope")]
    public string Scope { get; set; } = null!;

    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    [JsonPropertyName("created_at")]
    public long CreatedAt { get; set; }
}