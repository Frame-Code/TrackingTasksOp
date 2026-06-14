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
            var wps = await listsWorkPackagesCommand.Execute(new ListsWorkPackagesRequest(null, 0, 50));
            if (!wps.Any()) return "✅ ¡Felicidades! No tienes tareas pendientes asignadas en este momento.";

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
}
