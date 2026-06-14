using System.Text;
using Domain.Entities.OpenProjectEntities.WorkPackage;

namespace Infrastructure.Adapters.Services.Bot;

/// <summary>
/// Helpers de formateo de listados de work packages para las respuestas del bot.
/// </summary>
public static class WorkPackageFormatter
{
    /// <summary>
    /// Formatea work packages agrupados por proyecto, mostrando su estado actual.
    /// </summary>
    public static string FormatGroupedByProject(IEnumerable<WorkPackage> workPackages, string title = "📝 **Tus Tareas Pendientes:**")
    {
        var builder = new StringBuilder($"{title}\n\n");
        foreach (var group in workPackages.GroupBy(w => w.Links?.Project?.Title ?? "Otros Proyectos"))
        {
            builder.AppendLine($"📁 **{group.Key}**");
            foreach (var task in group) builder.AppendLine($"- **#{task.Id}**: {task.Subject} *(Estado: {task.Links?.Status?.Title})*");
            builder.AppendLine();
        }
        return builder.ToString().TrimEnd();
    }

    /// <summary>
    /// Formatea una lista plana de work packages bajo un título dado.
    /// </summary>
    public static string FormatFlatList(IEnumerable<WorkPackage> workPackages, string title)
    {
        var builder = new StringBuilder($"{title}\n\n");
        foreach (var task in workPackages) builder.AppendLine($"- **#{task.Id}**: {task.Subject}");
        return builder.ToString().TrimEnd();
    }
}
