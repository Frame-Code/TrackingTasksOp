using Application.Dto.Conversation;
using Application.Ports.UseCases.WorkPackages;

namespace Infrastructure.Adapters.Services.Bot.Actions;

public class AssignUserToTaskActionHandler(
    IUpdateWorkPackageCommand updateWorkPackageCommand,
    IOpenProjectEntityResolver entityResolver) : IBotActionHandler
{
    public string ActionName => "assign_user_to_task";

    public async Task<string> ExecuteAsync(GroqAction action, int? contextWpId, ConversationContext? conversationContext = null, CancellationToken ct = default)
    {
        var p = action.Params;
        int wpId = GroqActionParams.GetInt(p, "workPackageId", "id", "wpId");
        if (wpId == 0 && contextWpId.HasValue) wpId = contextWpId.Value;

        if (wpId <= 0) throw new Exception("Se requiere un ID de tarea válido (workPackageId).");

        string assigneeName = GroqActionParams.GetStr(p, "assigneeName", "assignee");
        string responsibleName = GroqActionParams.GetStr(p, "responsibleName", "responsible");

        int? assigneeId = await entityResolver.ResolveUserId(assigneeName);
        int? responsibleId = await entityResolver.ResolveUserId(responsibleName);

        if (!string.IsNullOrEmpty(assigneeName) && !assigneeId.HasValue)
            throw new Exception($"No pude encontrar al usuario '{assigneeName}' para asignarlo como responsable de ejecución. Verifica el nombre o usa 'listar usuarios del proyecto'.");

        if (!string.IsNullOrEmpty(responsibleName) && !responsibleId.HasValue)
            throw new Exception($"No pude encontrar al usuario '{responsibleName}' para asignarlo como responsable. Verifica el nombre o usa 'listar usuarios del proyecto'.");

        await updateWorkPackageCommand.Execute(wpId, assigneeId: assigneeId, responsibleId: responsibleId);
        return $"👤 Usuarios asignados correctamente a la tarea #{wpId}.";
    }
}
