namespace Infrastructure.Settings;

/// <summary>
/// Contiene la configuración para conectarse a Redis.
/// </summary>
public class RedisSettings
{
    public string Configuration { get; set; } = null!;
    public string InstanceName { get; set; } = null!;
    public int ConversationTtlMinutes { get; set; }
    public string KeyPrefix { get; set; } = null!;

    /// <summary>
    /// Días que se conserva un chat en el historial. Distinto de ConversationTtlMinutes,
    /// que solo pesaba sobre el contexto "caliente"; ahora los chats viven este tiempo.
    /// </summary>
    public int HistoryTtlDays { get; set; } = 30;
}
