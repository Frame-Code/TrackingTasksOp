using Domain.Entities.OpenProjectEntities.WorkPackage;
using Infrastructure.Adapters.Services.Bot;

namespace Tests.Infrastructure.Adapters.Services.Bot;

/// <summary>"¿Qué tengo pendiente hoy?" debe responder lo de hoy, no todo el backlog.</summary>
public class IsForTodayTests
{
    private static readonly DateOnly Today = new(2026, 8, 18);

    private static WorkPackage Wp(string start, string due) =>
        new() { Id = 1, StartDate = start, DueDate = due };

    [Theory]
    // vence hoy
    [InlineData("2026-08-10", "2026-08-18", true)]
    [InlineData("", "2026-08-18", true)]
    [InlineData("2026-08-18", "2026-08-18", true)]
    // la ventana de fechas incluye hoy
    [InlineData("2026-08-17", "2026-08-20", true)]
    // ya arrancó y no tiene fecha límite → en curso
    [InlineData("2026-08-01", "", true)]
    // vencida: sigue siendo trabajo pendiente, pero no es "de hoy" (ver IsOverdue)
    [InlineData("2026-08-10", "2026-08-15", false)]
    // todavía no arranca
    [InlineData("2026-08-25", "2026-08-30", false)]
    [InlineData("", "2026-08-30", false)]
    // sin fechas no hay forma de saber que sea de hoy
    [InlineData("", "", false)]
    public void IsForToday_ClasificaSegunLasFechasDeLaTarea(string start, string due, bool expected)
    {
        Assert.Equal(expected, HeuristicIntentInterceptor.IsForToday(Wp(start, due), Today));
    }

    [Fact]
    public void IsForToday_UnaVencidaYaNoArrastraTodoElBacklog()
    {
        // El bug: la regla era "due <= today", así que TODA tarea atrasada contaba como de hoy.
        // Con un backlog normal eso es casi todo, y la respuesta a "¿qué tengo pendiente HOY?"
        // terminaba siendo la lista completa — justo lo que la pregunta quiere evitar.
        var vencida = Wp("2026-08-01", "2026-08-15");

        Assert.False(HeuristicIntentInterceptor.IsForToday(vencida, Today));
        // No se oculta: se cuenta aparte y se menciona en una línea al pie del listado.
        Assert.True(HeuristicIntentInterceptor.IsOverdue(vencida, Today));
    }

    [Theory]
    [InlineData("2026-08-18", false)]  // vence hoy: todavía no está vencida
    [InlineData("2026-08-19", false)]  // vence mañana
    [InlineData("2026-08-17", true)]   // venció ayer
    [InlineData("", false)]            // sin fecha límite no puede estar vencida
    public void IsOverdue_SoloLoQueVencioAntesDeHoy(string due, bool expected)
    {
        Assert.Equal(expected, HeuristicIntentInterceptor.IsOverdue(Wp("2026-08-01", due), Today));
    }
}
