namespace Application.Ports.Auth;

public interface IAuthAuditLogger
{
    Task LogAsync(AuditEventType eventType, string? userId, object? detail, CancellationToken ct);
}
