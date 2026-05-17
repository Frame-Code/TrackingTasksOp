using Infrastructure.DataAccess.Entities.Enums;

namespace Infrastructure.DataAccess.Entities;

public class LocalCredential : UserCredential
{
    public string EncryptedApiKey { get; set; } = null!;
    public ApiKeyStatus ApiKeyStatus { get; set; }
    public DateTime ApiKeyLastValidatedAt { get; set; }
}

