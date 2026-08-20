namespace Infrastructure.Settings;

public class RateLimitSettings
{
    /// <summary>Límite general de la API: por usuario autenticado, o por IP si no hay sesión.</summary>
    public int PermitLimit { get; set; } = 120;
    public int WindowSeconds { get; set; } = 60;

    /// <summary>Límite más estricto para login/registro (sin sesión todavía, blanco típico de fuerza bruta/spam), por IP.</summary>
    public int AuthPermitLimit { get; set; } = 10;
    public int AuthWindowSeconds { get; set; } = 60;
}
