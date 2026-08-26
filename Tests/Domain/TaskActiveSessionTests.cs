using Domain.Entities.TrackingTasksEntities;
using Xunit;
using TaskEntity = Domain.Entities.TrackingTasksEntities.Task;

namespace Tests.DomainEntities;

/// <summary>
/// Antes, los casos de uso buscaban la sesion activa con
/// <c>OrderBy(StartTime).LastOrDefault()</c> y despues miraban si estaba abierta. Con una
/// entrada manual de hora posterior, esa consulta devuelve una sesion CERRADA y el sistema
/// concluye que no hay ninguna activa: pausar tiraba "doesn't have an active session" y
/// arrancar apilaba sesiones abiertas en vez de cerrar la anterior.
///
/// Paso en produccion el 2026-08-26 y quedaron cinco sesiones abiertas sobre la misma tarea.
/// </summary>
public class TaskActiveSessionTests
{
    private static TaskEntity ConTiempos(params TaskTimeDetail[] detalles) =>
        new() { WorkPackageId = 2745, Name = "tarea", TasksTimeDetails = detalles };

    [Fact]
    public void Una_sesion_cerrada_con_hora_posterior_no_oculta_la_abierta()
    {
        var task = ConTiempos(
            new TaskTimeDetail { Id = 1, StartTime = new DateTime(2026, 8, 26, 1, 17, 0) },
            // Cargada a mano, mas tarde en el reloj pero ya terminada.
            new TaskTimeDetail
            {
                Id = 2,
                StartTime = new DateTime(2026, 8, 26, 11, 0, 0),
                EndTime = new DateTime(2026, 8, 26, 13, 0, 0)
            });

        var activa = task.GetActiveSession();

        Assert.NotNull(activa);
        Assert.Equal(1, activa!.Id);
    }

    [Fact]
    public void Sin_sesiones_abiertas_devuelve_null()
    {
        var task = ConTiempos(new TaskTimeDetail
        {
            Id = 1,
            StartTime = new DateTime(2026, 8, 26, 9, 0, 0),
            EndTime = new DateTime(2026, 8, 26, 10, 0, 0)
        });

        Assert.Null(task.GetActiveSession());
    }

    /// <summary>Con datos ya corruptos (varias abiertas), se cierra la mas reciente.</summary>
    [Fact]
    public void Con_varias_abiertas_devuelve_la_mas_reciente()
    {
        var task = ConTiempos(
            new TaskTimeDetail { Id = 1, StartTime = new DateTime(2026, 8, 26, 1, 17, 0) },
            new TaskTimeDetail { Id = 2, StartTime = new DateTime(2026, 8, 26, 3, 6, 0) },
            new TaskTimeDetail { Id = 3, StartTime = new DateTime(2026, 8, 26, 1, 52, 0) });

        Assert.Equal(2, task.GetActiveSession()!.Id);
    }
}
