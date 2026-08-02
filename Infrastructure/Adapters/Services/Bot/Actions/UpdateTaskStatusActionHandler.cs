using Application.Dto.Conversation;
using Application.Ports.UseCases.WorkPackages;
using Infrastructure.Exceptions;

namespace Infrastructure.Adapters.Services.Bot.Actions;

public class UpdateTaskStatusActionHandler(
    IUpdateWorkPackageCommand updateWorkPackageCommand,
    IOpenProjectEntityResolver entityResolver) : IBotActionHandler
{
    public string ActionName => "update_task_status";

    public async Task<string> ExecuteAsync(GroqAction action, int? contextWpId, ConversationContext? conversationContext = null, CancellationToken ct = default)
    {
        var p = action.Params;
        int wpId = GroqActionParams.GetInt(p, "workPackageId", "id", "wpId");
        if (wpId == 0 && contextWpId.HasValue) wpId = contextWpId.Value;

        if (wpId <= 0) throw new Exception("Se requiere un ID de tarea válido (workPackageId).");
        int? updStatId = GroqActionParams.GetNullableInt(p, "statusId");
        if (!updStatId.HasValue) updStatId = await entityResolver.ResolveStatusId(GroqActionParams.GetStr(p, "statusName", "status"));

        if (!updStatId.HasValue) throw new Exception("No pude resolver el nombre del estado.");

        try
        {
            await updateWorkPackageCommand.Execute(wpId, statusId: updStatId.Value);
        }
        catch (InvalidStatusTransitionException ex)
        {
            throw new Exception($"No se pudo cambiar el estado de la tarea #{wpId}: {ex.Message}{ex.BuildSuggestion()}");
        }

        return $"🔄 Estado de la tarea #{wpId} actualizado correctamente.";
    }
}
