namespace Application.Ports.Auth;

public enum AuditEventType
{
    Login,
    Logout,
    Register,
    Exception,
    ApiKeyChanged,
    InvalidApiKey,
    OAuthGranted,
    OAuthRevoked,
    LoginFailed,
    PasswordReset
}