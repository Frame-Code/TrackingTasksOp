namespace Web.Infrastructure.Config.Settings;

/// <summary>
/// Contiene la configuración para conectarse a Redis.
/// </summary>
public class RedisSettings
{
    public required string Configuration { get; set; }
    public required string InstanceName { get; set; }
    public int ConversationTtlMinutes { get; set; } = 60; // 1 hora de TTL por defecto
    public required string KeyPrefix { get; set; }
}
