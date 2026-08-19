namespace Application.Dto.Auth;

/// <summary>
/// Identificadores estables de cada tipo de notificación recurrente. Coinciden a propósito
/// con el `tag` que ya usa el frontend (Web/wwwroot/js/timer.js) para las notificaciones del
/// navegador, así front y back comparten el mismo vocabulario sin un mapeo aparte.
/// </summary>
public static class NotificationTypeCodes
{
    public const string SessionReminder = "session-reminder";
    public const string PendingUploadReminder = "pending-upload-reminder";

    public const int DefaultIntervalMinutes = 15;

    public static readonly string[] All = [SessionReminder, PendingUploadReminder];
}
