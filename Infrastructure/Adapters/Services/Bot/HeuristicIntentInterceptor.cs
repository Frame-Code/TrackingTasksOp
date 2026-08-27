using System.Globalization;
using System.Linq;
using System.Text;
using Application.Dto.ListWorkPackages;
using Application.Ports.Services;
using Application.Ports.UseCases.WorkPackages;

namespace Infrastructure.Adapters.Services.Bot;

public class HeuristicIntentInterceptor(
    IProjectOpService projectOpService,
    IListsWorkPackagesCommand listsWorkPackagesCommand,
    IStatusOpService statusOpService) : IBotIntentInterceptor
{
    public string Normalize(string prompt)
    {
        string normalizedPrompt = prompt.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (var c in normalizedPrompt)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        }
        return sb.ToString().ToLowerInvariant().Trim();
    }

    public async Task<string?> TryInterceptAsync(string lowerPrompt, CancellationToken ct = default)
    {
        // Interceptar: Listar Proyectos
        if (lowerPrompt == "proyectos" || lowerPrompt.Contains("listar proyectos") || lowerPrompt.Contains("mis proyectos") || lowerPrompt.Contains("que proyectos"))
        {
            var projects = await projectOpService.Lists();
            string response = "📋 **Tus Proyectos Disponibles:**\n\n";
            foreach (var p in projects) response += $"- **{p.Name}** (ID: {p.Id})\n";
            return response;
        }

        // Interceptar: Listar Tareas Pendientes (Generales)
        // Si el prompt incluye un calificador (estado/status/proyecto), se delega al LLM
        // para que extraiga el filtro y se resuelva en ListTasksActionHandler.
        bool hasFilterQualifier = lowerPrompt.Contains("estado") || lowerPrompt.Contains("status") || lowerPrompt.Contains("proyecto");
        if (!hasFilterQualifier && (lowerPrompt == "tareas" || lowerPrompt.Contains("mis tareas") || lowerPrompt.Contains("tareas pendientes") || lowerPrompt.Contains("que tengo pendiente")))
        {
            // "Pendiente" es lo que sigue abierto: el listado general ahora trae también
            // las cerradas, así que aquí hay que pedir explícitamente solo las abiertas.
            var wps = await listsWorkPackagesCommand.Execute(new ListsWorkPackagesRequest(null, 0, 50, OnlyOpen: true));
            if (!wps.Any()) return "✅ ¡Felicidades! No tienes tareas pendientes asignadas en este momento.";

            // Si el usuario dijo "hoy", se responde lo de hoy — antes se listaba todo y
            // la respuesta contradecía la pregunta.
            if (lowerPrompt.Contains("hoy"))
            {
                var today = DateOnly.FromDateTime(DateTime.Today);
                var todayWps = wps.Where(wp => IsForToday(wp, today)).ToList();
                var overdue = wps.Count(wp => IsOverdue(wp, today));

                if (!todayWps.Any())
                    return $"✅ No tienes tareas con fecha para hoy ({today:dd/MM/yyyy})."
                         + OverdueNote(overdue)
                         + $"\n\nTienes **{wps.Count}** tarea(s) abiertas en total; escribe **mis tareas** para verlas todas.";

                return WorkPackageFormatter.FormatGroupedByProject(
                    todayWps, $"📅 **Tus tareas de hoy ({today:dd/MM/yyyy}):**")
                    + OverdueNote(overdue);
            }

            return WorkPackageFormatter.FormatGroupedByProject(wps);
        }

        // Interceptar: Listar Estados
        if (lowerPrompt == "estados" || lowerPrompt.Contains("listar estados") || lowerPrompt.Contains("ver estados"))
        {
            var statuses = await statusOpService.Lists();
            string response = "🚦 **Estados Disponibles:**\n\n";
            foreach (var s in statuses) response += $"- {s.Name} (ID: {s.Id})\n";
            return response;
        }

        return null;
    }

    /// <summary>
    /// Una tarea es "de hoy" si su ventana de fechas incluye hoy: ya arrancó (o no dice
    /// cuándo) y todavía no venció. Sin ninguna fecha no hay forma de saberlo, así que no entra.
    ///
    /// Las vencidas quedan FUERA a propósito. Antes contaban como "de hoy" con el argumento de
    /// que una tarea atrasada sigue siendo trabajo pendiente — cierto, pero con un backlog
    /// normal casi todo está vencido y el filtro terminaba devolviendo la lista entera, que es
    /// justo lo que la pregunta "¿qué tengo pendiente HOY?" quiere evitar. Se cuentan aparte
    /// con <see cref="IsOverdue"/> para que no desaparezcan de la vista.
    /// </summary>
    internal static bool IsForToday(Domain.Entities.OpenProjectEntities.WorkPackage.WorkPackage wp, DateOnly today)
    {
        var start = ParseDate(wp.StartDate);
        var due = ParseDate(wp.DueDate);

        if (due is not null && due < today) return false;                    // vencida: pendiente, pero no de hoy
        if (start is not null) return start <= today;                        // arrancó y no venció
        return due == today;                                                 // sin inicio: solo si vence hoy
    }

    /// <summary>Venció antes de hoy y sigue abierta.</summary>
    internal static bool IsOverdue(Domain.Entities.OpenProjectEntities.WorkPackage.WorkPackage wp, DateOnly today) =>
        ParseDate(wp.DueDate) is { } due && due < today;

    private static string OverdueNote(int count) => count switch
    {
        0 => "",
        1 => "\n\n⚠️ Además tienes **1** tarea vencida; escribe **mis tareas** para verla.",
        _ => $"\n\n⚠️ Además tienes **{count}** tareas vencidas; escribe **mis tareas** para verlas."
    };

    private static DateOnly? ParseDate(string? value) =>
        DateOnly.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : null;
}
