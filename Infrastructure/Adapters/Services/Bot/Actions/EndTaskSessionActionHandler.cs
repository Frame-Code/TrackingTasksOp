using Application.Dto.Conversation;
using Application.Dto.Tasks;
using Application.Ports.UseCases.Tasks;
using Infrastructure.Exceptions;

namespace Infrastructure.Adapters.Services.Bot.Actions;

public class EndTaskSessionActionHandler(
    IEndTaskSessionCommand endTaskSessionCommand,
    IOpenProjectEntityResolver entityResolver) : IBotActionHandler
{
    public string ActionName => "end_task_session";

    public async Task<string> ExecuteAsync(GroqAction action, int? contextWpId, ConversationContext? conversationContext = null, CancellationToken ct = default)
    {
        var p = action.Params;
        int wpId = GroqActionParams.GetInt(p, "workPackageId", "id", "wpId");
        if (wpId == 0 && contextWpId.HasValue) wpId = contextWpId.Value;

        if (wpId <= 0) throw new Exception("Se requiere un ID de tarea válido (workPackageId).");
        int? endStatId = GroqActionParams.GetNullableInt(p, "newStatusId");
        if (!endStatId.HasValue) endStatId = await entityResolver.ResolveStatusId(GroqActionParams.GetStr(p, "newStatusName", "newStatus"));

        try
        {
            await endTaskSessionCommand.Execute(new EndTaskSessionRequest(wpId, GroqActionParams.GetNullableInt(p, "activityId", "activity"), GroqActionParams.GetStr(p, "comment"), endStatId));
        }
        catch (TaskStatusTransitionException ex)
        {
            return $"⏹️ Sesión de la tarea #{wpId} finalizada y horas registradas. ⚠️ {ex.Message}";
        }

        return $"⏹️ Sesión de la tarea #{wpId} finalizada y horas registradas.";
    }
}
