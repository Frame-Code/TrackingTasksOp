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
    // vence hoy o ya venció → sigue siendo trabajo de hoy
    [InlineData("2026-08-10", "2026-08-18", true)]
    [InlineData("2026-08-10", "2026-08-15", true)]
    [InlineData("", "2026-08-18", true)]
    // la ventana de fechas incluye hoy
    [InlineData("2026-08-17", "2026-08-20", true)]
    [InlineData("2026-08-18", "2026-08-18", true)]
    // ya arrancó y no tiene fecha límite → en curso
    [InlineData("2026-08-01", "", true)]
    // todavía no arranca
    [InlineData("2026-08-25", "2026-08-30", false)]
    [InlineData("", "2026-08-30", false)]
    // sin fechas no hay forma de saber que sea de hoy
    [InlineData("", "", false)]
    public void IsForToday_ClasificaSegunLasFechasDeLaTarea(string start, string due, bool expected)
    {
        Assert.Equal(expected, HeuristicIntentInterceptor.IsForToday(Wp(start, due), Today));
    }
}
