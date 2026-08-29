namespace Domain.Entities.TrackingTasksEntities;

public class TaskTimeDetail
{
    public int Id { get; set; }
    public DateTime StartTime { get; init; } = DateTime.Now;
    public DateTime? EndTime { get; set; }
    public bool Uploaded { get; set; } = false;
    public string UserId { get; set; } = null!;
    public int IdTask  { get; set; }
    public Task Task { get; set; } = null!;

    /// <summary>
    /// Último instante en que el cliente confirmó que la sesión seguía viva. Lo sella el
    /// servidor, no el navegador: un reloj de cliente adelantado no puede inflar horas.
    ///
    /// Es la única evidencia real de hasta cuándo se estuvo trabajando. Sin esto, cerrar una
    /// sesión huérfana obliga a inventar un final.
    /// </summary>
    public DateTime? LastHeartbeat { get; set; }

    /// <summary>
    /// La sesión no la cerró el usuario: la cerró <see cref="CloseAsUnconfirmed"/> a partir del
    /// último latido. El tiempo es una estimación, así que NO puede subirse sola a OpenProject —
    /// va a la cola de pendientes para que el usuario la confirme o la corrija.
    /// </summary>
    public bool EndTimeInferred { get; set; } = false;

    public TimeSpan? GetHoursWorked() =>
        EndTime - StartTime;

    /// <summary>
    /// Cierra una sesión que quedó abierta (se apagó el servidor, se cerró el navegador, el
    /// usuario nunca le dio finalizar) usando el último momento con evidencia de actividad.
    ///
    /// Antes esto se hacía con <c>DateTime.Now</c> en el momento en que alguien se daba cuenta,
    /// lo que contabilizaba como trabajadas todas las horas en que nadie estuvo trabajando —
    /// con apagado nocturno del servidor, una jornada entera cada vez.
    ///
    /// Sin ningún latido no hay evidencia de que se haya trabajado un solo minuto, así que el
    /// cierre es en <see cref="StartTime"/>: duración cero. Cero es honesto; ocho horas no.
    /// </summary>
    public void CloseAsUnconfirmed()
    {
        EndTime = LastHeartbeat ?? StartTime;
        EndTimeInferred = true;
        Uploaded = false;
    }
}
