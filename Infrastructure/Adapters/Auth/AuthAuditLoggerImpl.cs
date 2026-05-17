using System.Text.Json;
using Application.Ports.Auth;
using Infrastructure.DataAccess;
using Infrastructure.DataAccess.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Adapters.Auth;

public class AuthAuditLoggerImpl(
    TrackingTasksDbContext context,
    IHttpContextAccessor accessor,
    ILogger<AuthAuditLoggerImpl> logger) : IAuthAuditLogger
{
    public async Task LogAsync(AuditEventType eventType, string? userId, object? detail, CancellationToken ct)
    {
        try
        {
            var httpContext = accessor.HttpContext;
            var ip = httpContext?.Connection?.RemoteIpAddress?.ToString() ?? "unknown";
            var ua = httpContext?.Request.Headers.UserAgent.ToString() ?? "unknown";
            
            var log = new AuthAuditLog
            {
                EventType = eventType,
                UserId = userId,
                IpAddress = Truncate(ip, 45),
                UserAgent = Truncate(ua, 500),
                Detail = detail is null ? "{}" : JsonSerializer.Serialize(detail),
                CreatedAt = DateTime.UtcNow
            };
            
            await context.AuthAuditLogs.AddAsync(log, ct);
            await context.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to write auth audit log {EventType} for {UserId}",  eventType, userId);
        }
    }
    
    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : s[..max];
}