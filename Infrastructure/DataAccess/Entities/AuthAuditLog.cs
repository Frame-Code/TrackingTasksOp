using Infrastructure.DataAccess.Entities.Enums;

namespace Infrastructure.DataAccess.Entities;

public class AuthAuditLog
{
    public long Id { get; set; }
    public string? UserId { get; set; }
    public AuditEventType EventType { get; set; }
    public string IpAddress { get; set; } = null!;
    public string UserAgent  { get; set; } = null!;
    public string Detail { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
}