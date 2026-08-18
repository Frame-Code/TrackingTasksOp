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

                if (!todayWps.Any())
                    return $"✅ No tienes tareas con fecha para hoy ({today:dd/MM/yyyy}).\n\n"
                         + $"Tienes **{wps.Count}** tarea(s) abiertas en total; escribe **mis tareas** para verlas todas.";

                return WorkPackageFormatter.FormatGroupedByProject(
                    todayWps, $"📅 **Tus tareas de hoy ({today:dd/MM/yyyy}):**");
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
    /// Una tarea cuenta como "de hoy" si vence hoy o ya venció (sigue siendo trabajo
    /// pendiente de hoy), si su ventana de fechas incluye hoy, o si ya arrancó y no
    /// tiene fecha límite. Sin ninguna fecha no hay forma de saberlo, así que no entra.
    /// </summary>
    internal static bool IsForToday(Domain.Entities.OpenProjectEntities.WorkPackage.WorkPackage wp, DateOnly today)
    {
        var start = ParseDate(wp.StartDate);
        var due = ParseDate(wp.DueDate);

        if (due is not null && due <= today) return true;                    // vence hoy o vencida
        if (start is null || start > today) return false;                    // aún no arranca
        return due is null || today <= due;                                  // en curso hoy
    }

    private static DateOnly? ParseDate(string? value) =>
        DateOnly.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : null;
}
