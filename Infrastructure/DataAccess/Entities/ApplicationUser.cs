using Domain.Entities.TrackingTasksEntities;
using Infrastructure.DataAccess.Entities.Enums;
using Microsoft.AspNetCore.Identity;

namespace Infrastructure.DataAccess.Entities;

public class ApplicationUser : IdentityUser
{
    public int OpenProjectUserId { get; set; }
    public int OpenProjectInstanceId { get; set; }
    public OpenProjectInstance OpenProjectInstance { get; set; } = null!;
    public string OpenProjectInstanceBaseUrl { get; set; } = null!;
    public AuthMethod AuthMethod { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    /// <summary>Qué hacer al pausar una tarea sin preguntar cada vez. "Ask" = sigue preguntando (default).</summary>
    public PauseDefaultBehavior PauseDefaultBehavior { get; set; } = PauseDefaultBehavior.Ask;

    /// <summary>Si es true, cancelar una sesión no pide confirmación antes de descartar el tiempo.</summary>
    public bool SkipCancelConfirmation { get; set; } = false;

    /// <summary>
    /// Si es true (default, comportamiento histórico), al finalizar una sesión se le suma un
    /// margen aleatorio al tiempo registrado en OpenProject. Si es false, se envían los minutos
    /// exactos trackeados.
    /// </summary>
    public bool AddRandomSlackTime { get; set; } = true;

    /// <summary>
    /// API key propia del usuario para el bot de IA (cifrada). Null = usa la key compartida
    /// del servidor, sujeta al límite de uso diario (ver IAiUsageLimiter). Con key propia no
    /// hay límite: el costo/cuota corre por cuenta del usuario.
    /// </summary>
    public string? EncryptedGroqApiKey { get; set; }
}