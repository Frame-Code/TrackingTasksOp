using Domain.Entities.TrackingTasksEntities;

namespace Tests.DomainEntities;

/// <summary>
/// El defecto que motivó esto: una sesión abierta a las 00:50 que el servidor cortó a la 1:00
/// se cerraba con DateTime.Now al retomarla al día siguiente, contabilizando como trabajadas
/// todas las horas en que el servicio estuvo caído — y se subía sola a OpenProject.
/// </summary>
public class TaskTimeDetailCloseAsUnconfirmedTests
{
    [Fact]
    public void CloseAsUnconfirmed_CierraEnElUltimoLatido_NoEnLaHoraActual()
    {
        var inicio = new DateTime(2026, 8, 29, 0, 50, 0);
        var detail = new TaskTimeDetail
        {
            StartTime = inicio,
            UserId = "user-1",
            LastHeartbeat = new DateTime(2026, 8, 29, 1, 0, 0) // ultimo latido antes del apagado
        };

        detail.CloseAsUnconfirmed();

        Assert.Equal(new DateTime(2026, 8, 29, 1, 0, 0), detail.EndTime);
        Assert.Equal(TimeSpan.FromMinutes(10), detail.GetHoursWorked());
    }

    [Fact]
    public void CloseAsUnconfirmed_SinNingunLatido_DuracionCero()
    {
        // Sin latidos no hay evidencia de que se trabajara un solo minuto. Cero es honesto.
        var inicio = new DateTime(2026, 8, 29, 0, 50, 0);
        var detail = new TaskTimeDetail { StartTime = inicio, UserId = "user-1", LastHeartbeat = null };

        detail.CloseAsUnconfirmed();

        Assert.Equal(inicio, detail.EndTime);
        Assert.Equal(TimeSpan.Zero, detail.GetHoursWorked());
    }

    [Fact]
    public void CloseAsUnconfirmed_NuncaQuedaMarcadaComoSubida()
    {
        // Un tiempo estimado no se publica en OpenProject sin que el usuario lo confirme.
        var detail = new TaskTimeDetail
        {
            StartTime = new DateTime(2026, 8, 29, 0, 50, 0),
            UserId = "user-1",
            LastHeartbeat = new DateTime(2026, 8, 29, 1, 0, 0),
            Uploaded = true
        };

        detail.CloseAsUnconfirmed();

        Assert.False(detail.Uploaded);
        Assert.True(detail.EndTimeInferred);
    }

    [Fact]
    public void CloseAsUnconfirmed_NoDependeDeCuandoSeEjecuta()
    {
        // La garantía de fondo: el resultado sale de la evidencia guardada, no del reloj. Da
        // igual que la reconciliación corra un minuto o una semana después del apagado.
        static TaskTimeDetail Nuevo() => new()
        {
            StartTime = new DateTime(2026, 8, 29, 0, 50, 0),
            UserId = "user-1",
            LastHeartbeat = new DateTime(2026, 8, 29, 1, 0, 0)
        };

        var a = Nuevo();
        var b = Nuevo();
        a.CloseAsUnconfirmed();
        b.CloseAsUnconfirmed();

        Assert.Equal(a.EndTime, b.EndTime);
    }
}
