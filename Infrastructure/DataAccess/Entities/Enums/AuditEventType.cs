namespace Infrastructure.DataAccess.Entities.Enums;

public enum AuditEventType
{
    Login,
    Logout,
    Register,
    ApiKeyChanged,
    InvalidApiKey,
    OAuthGranted,
    OAuthRevoked,
    LoginFailed,
    PasswordReset
}