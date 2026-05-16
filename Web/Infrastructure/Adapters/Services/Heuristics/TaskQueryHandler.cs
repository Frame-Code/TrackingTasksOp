using System.Text;
using System.Text.RegularExpressions;
using Application.Dto.ListWorkPackages;
using Application.Ports.Services;
using Application.Ports.UseCases.WorkPackages;

namespace Web.Infrastructure.Adapters.Services.Heuristics;

public class TaskQueryHandler(
    IListsWorkPackagesCommand listsWorkPackagesCommand,
    IStatusOpService statusOpService,
    IProjectOpService projectOpService) : IHeuristicIntentHandler
{
    public async Task<string?> HandleAsync(string prompt)
    {
        string lower = prompt.ToLowerInvariant().Trim();
        
        // No interceptar si parece una intención de creación o acción
        if (lower.Contains("crea") || lower.Contains("nueva") || lower.Contains("inicia") || 
            lower.Contains("comienza") || lower.Contains("pon") || lower.Contains("asigna") ||
            lower.Contains("finaliza") || lower.Contains("termina") || lower.Contains("detalla")) 
            return null;

        if (!lower.Contains("tarea") && !lower.Contains("pendiente") && !lower.Contains("listado")) return null;

        bool isMine = lower.Contains("mis tareas") || lower.Contains("mis pendientes") || lower.Contains("qué tengo pendiente") || lower.Contains("asignadas a mi");
        string? statusOperator = "o"; // Por defecto abiertas
        int? statusId = null;
        int? projectId = null;
        string titlePrefix = isMine ? "Tus Tareas" : "Tareas";
        string titleSuffix = "Pendientes (Abiertas)";

        // 1. Buscar Proyecto por contenido en el prompt
        var allProjects = await projectOpService.Lists();
        var matchedProject = allProjects.FirstOrDefault(p => lower.Contains(p.Name.ToLowerInvariant()));
        if (matchedProject != null)
        {
            projectId = matchedProject.Id;
            titleSuffix = $" del proyecto '{matchedProject.Name}'";
        }

        // 2. Buscar Estado por contenido en el prompt
        var allStatuses = await statusOpService.Lists();
        var matchedStatus = allStatuses.FirstOrDefault(s => lower.Contains(s.Name.ToLowerInvariant()));
        
        if (matchedStatus != null)
        {
            statusId = matchedStatus.Id;
            statusOperator = null;
            titleSuffix = $"en estado '{matchedStatus.Name}'";
        }
        else if (lower.Contains("tareas abiertas") || lower.Contains("pendientes abiertas"))
        {
            statusOperator = "o";
            titleSuffix = "Abiertas";
        }
        else if (lower.Contains("tareas cerradas"))
        {
            statusOperator = "c";
            titleSuffix = "Cerradas";
        }

        var request = new ListsWorkPackagesRequest(
            AssigneeId: isMine ? "me" : null,
            StatusOperator: statusOperator,
            StatusId: statusId,
            ProjectId: projectId
        );

        var wps = await listsWorkPackagesCommand.Execute(request);
        if (!wps.Any()) return "✅ No se encontraron tareas con esos criterios.";

        var title = $"📝 **{titlePrefix} {titleSuffix}:**";
        var builder = new StringBuilder(title + "\n\n");
        foreach (var g in wps.GroupBy(w => w.Links?.Project?.Title ?? "Otros"))
        {
            builder.AppendLine($"📁 **{g.Key}**");
            foreach (var t in g) builder.AppendLine($"- **#{t.Id}**: {t.Subject} *({t.Links?.Status?.Title})*");
            builder.AppendLine();
        }
        return builder.ToString().TrimEnd();
    }
}
