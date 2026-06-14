using Application.Dto.Tasks;
using Application.Dto.WorkPackages;
using Application.Ports.Services;
using Application.Ports.UseCases.Tasks;
using Application.Ports.UseCases.WorkPackages;

namespace Infrastructure.Adapters.Services.Bot.Actions;

public class StartTaskActionHandler(
    IStartTaskCommand startTaskCommand,
    IStatusOpService statusOpService,
    IOpenProjectEntityResolver entityResolver,
    ICreateWorkPackageCommand createWorkPackageCommand) : IBotActionHandler
{
    public string ActionName => "start_task";

    public async Task<string> ExecuteAsync(GroqAction action, int? contextWpId, CancellationToken ct = default)
    {
        var p = action.Params;
        int wpId = GroqActionParams.GetInt(p, "workPackageId", "id", "wpId");
        if (wpId == 0 && contextWpId.HasValue) wpId = contextWpId.Value;

        int? projId = GroqActionParams.GetNullableInt(p, "projectId");
        string pName = GroqActionParams.GetStr(p, "projectName", "project");
        if (!projId.HasValue && !string.IsNullOrEmpty(pName))
        {
            projId = await entityResolver.ResolveProjectId(pName);
        }

        int? statId = GroqActionParams.GetNullableInt(p, "statusId");
        string statusToResolve = GroqActionParams.GetStr(p, "statusName", "status");
        if (!statId.HasValue && !string.IsNullOrEmpty(statusToResolve))
        {
            statId = await entityResolver.ResolveStatusId(statusToResolve);
        }

        // Si no hay estado, intentar usar "En curso" o el primero disponible como fallback
        if (!statId.HasValue && string.IsNullOrEmpty(statusToResolve))
        {
            var allStatuses = await statusOpService.Lists();
            statId = allStatuses.FirstOrDefault(s => s.Name.Contains("nuevo", StringComparison.OrdinalIgnoreCase))?.Id
                     ?? allStatuses.FirstOrDefault()?.Id;
        }

        string taskName = GroqActionParams.GetStr(p, "name", "subject", "title");
        if (string.IsNullOrEmpty(taskName)) throw new Exception("El parámetro 'name' (nombre de la tarea) es obligatorio.");
        if (!projId.HasValue) throw new Exception($"No pude encontrar el proyecto '{pName}'. Por favor, verifica el nombre o usa 'listar proyectos'.");
        if (!statId.HasValue) throw new Exception($"No pude encontrar el estado '{statusToResolve}'. Por favor, verifica el nombre o usa 'listar estados'.");

        // Si vamos a crear un work package nuevo (no a iniciar seguimiento de uno existente),
        // verificamos si el tipo de tarea del proyecto tiene campos personalizados obligatorios.
        Dictionary<string, int>? customFieldOptionIds = null;
        if (wpId <= 0)
        {
            var requiredFields = await createWorkPackageCommand.GetRequiredCustomFieldsAsync(projId.Value);
            if (requiredFields.Count > 0)
            {
                var providedCustomFields = GroqActionParams.GetDict(p, "customFields");
                var resolved = new Dictionary<string, int>();
                var missing = new List<RequiredCustomField>();

                foreach (var field in requiredFields)
                {
                    var providedValue = providedCustomFields?
                        .FirstOrDefault(kv => kv.Key.Equals(field.Name, StringComparison.OrdinalIgnoreCase)).Value;

                    var matchedOption = !string.IsNullOrEmpty(providedValue)
                        ? field.AllowedValues.FirstOrDefault(o =>
                            o.Value.Equals(providedValue, StringComparison.OrdinalIgnoreCase) ||
                            o.Value.Contains(providedValue, StringComparison.OrdinalIgnoreCase))
                        : null;

                    if (matchedOption != null) resolved[field.Key] = matchedOption.Id;
                    else missing.Add(field);
                }

                if (missing.Count > 0)
                {
                    var msg = "🤔 Para crear esta tarea necesito que me indiques los siguientes datos:\n\n";
                    foreach (var f in missing)
                        msg += $"- **{f.Name}**: {string.Join(", ", f.AllowedValues.Select(o => o.Value))}\n";
                    msg += "\nIndícame estos valores y vuelvo a crear la tarea.";
                    return msg;
                }

                customFieldOptionIds = resolved;
            }
        }

        var startReq = new StarTaskRequest
        {
            ProjectId = projId.Value,
            StatusId = statId.Value,
            Name = taskName,
            WorkPackageId = wpId,
            Description = GroqActionParams.GetStr(p, "description"),
            ActivityId = GroqActionParams.GetNullableInt(p, "activityId"),
            Comment = GroqActionParams.GetStr(p, "comment"),
            AssigneeId = await entityResolver.ResolveUserId(GroqActionParams.GetStr(p, "assigneeName", "assignee"), projId),
            ResponsibleId = await entityResolver.ResolveUserId(GroqActionParams.GetStr(p, "responsibleName", "responsible"), projId),
            StartDate = GroqActionParams.GetDate(p, "startDate"),
            DueDate = GroqActionParams.GetDate(p, "dueDate"),
            CustomFieldOptionIds = customFieldOptionIds
        };
        var newTask = await startTaskCommand.Execute(startReq);
        var message = $"🚀 Tarea **{newTask.Name}** preparada y seguimiento iniciado (ID: {newTask.WorkPackageId}).";

        // Prevención de errores (heurística de Nielsen): si se creó una tarea nueva sin asignar
        // a nadie, avisamos de inmediato, ya que no aparecerá en las vistas "Mis tareas" de OpenProject.
        if (wpId <= 0 && startReq.AssigneeId is null && startReq.ResponsibleId is null)
        {
            message += "\n\n⚠️ No indicaste a quién asignar la tarea, así que quedó sin asignado y no aparecerá en 'Mis tareas' de OpenProject. " +
                       "Si quieres, dime a quién asignarla (puede ser a ti mismo).";
        }

        return message;
    }
}
