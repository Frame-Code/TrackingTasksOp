using Application.Dto.Conversation;
using Application.Dto.Tasks;
using Application.Ports.UseCases.Tasks;

namespace Infrastructure.Adapters.Services.Bot.Actions;

public class PauseTaskActionHandler(
    IPauseTaskCommand pauseTaskCommand,
    IOpenProjectEntityResolver entityResolver) : IBotActionHandler
{
    public string ActionName => "pause_task";

    public async Task<string> ExecuteAsync(GroqAction action, int? contextWpId, ConversationContext? conversationContext = null, CancellationToken ct = default)
    {
        var p = action.Params;
        int wpId = GroqActionParams.GetInt(p, "workPackageId", "id", "wpId");
        if (wpId == 0 && contextWpId.HasValue) wpId = contextWpId.Value;
        if (wpId <= 0) throw new Exception("Se requiere un ID de tarea válido (workPackageId).");

        string statusToResolve = GroqActionParams.GetStr(p, "statusName", "status");
        if (string.IsNullOrEmpty(statusToResolve)) statusToResolve = "On hold";

        int? onHoldStatusId = await entityResolver.ResolveStatusId(statusToResolve);

        // Por defecto sube el tiempo a OpenProject; el LLM manda uploadNow=false si el usuario
        // pidió guardarlo en local para retomarlo después.
        bool uploadNow = GroqActionParams.GetBool(p, "uploadNow", fallback: true);

        await pauseTaskCommand.Execute(new PauseTaskRequest(wpId, onHoldStatusId, uploadNow));
        return uploadNow
            ? $"⏸️ Tarea #{wpId} pausada. El tiempo transcurrido se registró en OpenProject."
            : $"⏸️ Tarea #{wpId} pausada. El tiempo quedó guardado en local; se subirá cuando la retomes y finalices.";
    }
}
