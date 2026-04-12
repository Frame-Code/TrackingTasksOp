namespace Web.Infrastructure.Config.Settings;

/// <summary>
/// Contiene la configuración para conectarse a Redis.
/// </summary>
public class RedisSettings
{
    public string Configuration { get; set; } = null!;
    public string InstanceName { get; set; } = null!;
    public int ConversationTtlMinutes { get; set; }
    public string KeyPrefix { get; set; } = null!;
}
