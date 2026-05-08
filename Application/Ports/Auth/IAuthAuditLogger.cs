namespace Application.Ports.Auth;

public interface IAuthAuditLogger
{
    Task LogAsync();
}
