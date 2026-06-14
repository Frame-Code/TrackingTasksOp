using Application.Ports.UseCases.WorkPackages;

namespace Infrastructure.Adapters.Services.Bot.Actions;

public class AssignUserToTaskActionHandler(
    IUpdateWorkPackageCommand updateWorkPackageCommand,
    IOpenProjectEntityResolver entityResolver) : IBotActionHandler
{
    public string ActionName => "assign_user_to_task";

    public async Task<string> ExecuteAsync(GroqAction action, int? contextWpId, CancellationToken ct = default)
    {
        var p = action.Params;
        int wpId = GroqActionParams.GetInt(p, "workPackageId", "id", "wpId");
        if (wpId == 0 && contextWpId.HasValue) wpId = contextWpId.Value;

        if (wpId <= 0) throw new Exception("Se requiere un ID de tarea válido (workPackageId).");
        await updateWorkPackageCommand.Execute(wpId,
            assigneeId: await entityResolver.ResolveUserId(GroqActionParams.GetStr(p, "assigneeName", "assignee")),
            responsibleId: await entityResolver.ResolveUserId(GroqActionParams.GetStr(p, "responsibleName", "responsible")));
        return $"👤 Usuarios asignados correctamente a la tarea #{wpId}.";
    }
}
