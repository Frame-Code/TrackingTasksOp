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
    /// Si es true (default), al finalizar una sesión el tiempo registrado en OpenProject se
    /// redondea hacia arriba al siguiente cuarto de hora (ver
    /// <see cref="Infrastructure.Adapters.Services.TimeTrackService.RoundUpToQuarterHour"/>).
    /// Si es false, se envían los minutos exactos trackeados.
    ///
    /// ponytail: el nombre quedó de cuando el margen era aleatorio; ahora es determinista.
    /// Renombrarlo cuesta una migración de columna más el contrato JSON con el front (28
    /// referencias), así que se difiere hasta que haya otro motivo para tocar esa tabla.
    /// </summary>
    public bool AddRandomSlackTime { get; set; } = true;

    /// <summary>
    /// API key propia del usuario para el bot de IA (cifrada). Null = usa la key compartida
    /// del servidor, sujeta al límite de uso diario (ver IAiUsageLimiter). Con key propia no
    /// hay límite: el costo/cuota corre por cuenta del usuario.
    /// </summary>
    public string? EncryptedGroqApiKey { get; set; }

    /// <summary>
    /// IDs de estado de OpenProject (separados por coma) que se aplican como filtro apenas
    /// carga "Cargar tareas", en vez de mostrar todos los estados. Null/vacío = sin filtro
    /// por defecto (comportamiento actual).
    /// </summary>
    public string? DefaultStatusFilterIds { get; set; }
}