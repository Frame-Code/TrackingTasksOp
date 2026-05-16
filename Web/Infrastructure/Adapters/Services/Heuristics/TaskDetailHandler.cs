using System.Text;
using System.Text.RegularExpressions;
using Application.Ports.Services;
using Application.Ports.UseCases.WorkPackages;

namespace Web.Infrastructure.Adapters.Services.Heuristics;

public class TaskDetailHandler(
    IGetWorkPackageCommand getWorkPackageCommand,
    IAttachmentService attachmentService) : IHeuristicIntentHandler
{
    private static readonly Regex TaskIdRegex = new(@"(?:detalle|detalla|det[aá]llame|ver|mostrar|info|informaci[oó]n).*?tarea\s+#?(\d+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public async Task<string?> HandleAsync(string prompt)
    {
        var match = TaskIdRegex.Match(prompt);
        if (match.Success && int.TryParse(match.Groups[1].Value, out int taskId))
        {
            var wp = await getWorkPackageCommand.Execute(taskId);
            if (wp == null) return $"❌ No se encontró la tarea **#{taskId}**.";

            var builder = new StringBuilder();
            builder.AppendLine($"📌 **Detalle de Tarea #{wp.Id}**");
            builder.AppendLine($"**Asunto:** {wp.Subject}");
            builder.AppendLine($"**Estado:** {wp.Links?.Status?.Title ?? "N/A"}");
            builder.AppendLine($"**Proyecto:** {wp.Links?.Project?.Title ?? "N/A"}");
            builder.AppendLine($"**Prioridad:** {wp.Links?.Priority?.Title ?? "N/A"}");
            builder.AppendLine($"**Asignado a:** {wp.Links?.Assignee?.Title ?? "Sin asignar"}");
            
            if (!string.IsNullOrWhiteSpace(wp.Description?.Raw))
            {
                builder.AppendLine("\n**Descripción:**");
                builder.AppendLine(wp.Description.Raw);
            }

            // Buscar Adjuntos (Imágenes)
            var attachments = await attachmentService.GetAttachmentsAsync(taskId);
            var images = attachments.Where(a => a.ContentType.StartsWith("image/")).ToList();
            
            if (images.Any())
            {
                builder.AppendLine("\n🖼️ **Imágenes Adjuntas:**");
                foreach (var img in images)
                {
                    // Usamos una sintaxis especial que el frontend reconocerá
                    builder.AppendLine($"[image:{img.Id}]");
                }
            }

            if (!string.IsNullOrWhiteSpace(wp.StartDate) || !string.IsNullOrWhiteSpace(wp.DueDate))
            {
                string start = string.IsNullOrWhiteSpace(wp.StartDate) ? "---" : wp.StartDate;
                string due = string.IsNullOrWhiteSpace(wp.DueDate) ? "---" : wp.DueDate;
                builder.AppendLine($"\n**Fechas:** {start} a {due}");
            }

            return builder.ToString();
        }
        return null;
    }
}
