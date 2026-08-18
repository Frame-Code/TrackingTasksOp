namespace Infrastructure.DataAccess.Entities;

/// <summary>
/// Preferencia de un usuario para un tipo de notificación recurrente (ver
/// Application.Dto.Auth.NotificationTypeCodes). Ausencia de fila para un tipo = valores
/// por defecto (activada, cada 15 min); solo se guarda fila cuando el usuario se aparta
/// de eso, así la tabla no se llena de filas idénticas al default.
/// </summary>
public class UserNotificationSetting
{
    public string UserId { get; set; } = null!;
    public string TypeCode { get; set; } = null!;
    public bool Enabled { get; set; }
    public int IntervalMinutes { get; set; }

    /// <summary>
    /// Hoy solo existe "Interval" (repetir cada N minutos). Se deja la columna desde ya para
    /// que un futuro "hora fija del día" sea una migración aditiva, no un rediseño de tabla.
    /// </summary>
    public string ScheduleType { get; set; } = "Interval";
}
