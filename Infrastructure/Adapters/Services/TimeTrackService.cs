namespace Infrastructure.Adapters.Services;

public static class TimeTrackService
{
    private const int QuarterHourMinutes = 15;

    /// <summary>
    /// Redondea el tiempo trackeado hacia arriba al siguiente cuarto de hora.
    ///
    /// Modela el tiempo que el cronómetro no llega a medir en los bordes de la sesión: se
    /// arranca a pensar la tarea antes de darle play y se sigue un rato después de parar. Ese
    /// sobrante es acotado, no proporcional — una sesión de seis horas no tiene seis veces el
    /// arranque de una de una hora —, así que el redondeo a un bloque fijo lo representa mejor
    /// que un porcentaje. De paso deja el registro en los mismos incrementos en que se cargan
    /// las horas a mano en OpenProject.
    ///
    /// Reemplaza a la holgura aleatoria anterior, que tenía dos defectos: usaba
    /// <c>TimeSpan.Minutes</c> (el componente 0-59) en vez de <c>TotalMinutes</c>, con lo que el
    /// margen dependía del resto y no de la duración — 2 h exactas no recibían nada y 2 h 30
    /// sí —, y su rama de 20-40 minutos era inalcanzable porque comparaba ese mismo componente
    /// contra 60.
    /// </summary>
    public static TimeSpan RoundUpToQuarterHour(TimeSpan tracked)
    {
        // Los segundos se descartan antes de redondear. Si no, parar el cronómetro tres
        // segundos después de los 30 minutos exactos saltaría el bloque entero, hasta 45.
        var minutes = (int)Math.Floor(tracked.TotalMinutes);

        // Una sesión de menos de un minuto es un clic accidental: no hay nada que redondear
        // hacia arriba, y convertirla en 15 minutos sería inventar tiempo, no recuperarlo.
        if (minutes <= 0) return TimeSpan.Zero;

        var blocks = (int)Math.Ceiling(minutes / (double)QuarterHourMinutes);
        return TimeSpan.FromMinutes(blocks * QuarterHourMinutes);
    }
}
