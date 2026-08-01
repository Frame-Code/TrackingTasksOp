using Application.Dto.Conversation;
using Application.Dto.Tasks;
using Application.Ports.UseCases.Tasks;

namespace Infrastructure.Adapters.Services.Bot.Actions;

public class ResumeTaskActionHandler(
    IResumeTaskCommand resumeTaskCommand,
    IOpenProjectEntityResolver entityResolver) : IBotActionHandler
{
    public string ActionName => "resume_task";

    public async Task<string> ExecuteAsync(GroqAction action, int? contextWpId, ConversationContext? conversationContext = null, CancellationToken ct = default)
    {
        var p = action.Params;
        int wpId = GroqActionParams.GetInt(p, "workPackageId", "id", "wpId");
        if (wpId == 0 && contextWpId.HasValue) wpId = contextWpId.Value;
        if (wpId <= 0) throw new Exception("Se requiere un ID de tarea válido (workPackageId).");

        string statusToResolve = GroqActionParams.GetStr(p, "statusName", "status");
        if (string.IsNullOrEmpty(statusToResolve)) statusToResolve = "In progress";

        int? inProgressStatusId = await entityResolver.ResolveStatusId(statusToResolve);

        await resumeTaskCommand.Execute(new ResumeTaskRequest(wpId, inProgressStatusId));
        return $"▶️ Tarea #{wpId} reanudada. El seguimiento de tiempo continúa.";
    }
}
