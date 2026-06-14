using Application.Dto.Tasks;
using Application.Ports.UseCases.Tasks;

namespace Infrastructure.Adapters.Services.Bot.Actions;

public class PauseTaskActionHandler(
    IPauseTaskCommand pauseTaskCommand,
    IOpenProjectEntityResolver entityResolver) : IBotActionHandler
{
    public string ActionName => "pause_task";

    public async Task<string> ExecuteAsync(GroqAction action, int? contextWpId, CancellationToken ct = default)
    {
        var p = action.Params;
        int wpId = GroqActionParams.GetInt(p, "workPackageId", "id", "wpId");
        if (wpId == 0 && contextWpId.HasValue) wpId = contextWpId.Value;
        if (wpId <= 0) throw new Exception("Se requiere un ID de tarea válido (workPackageId).");

        string statusToResolve = GroqActionParams.GetStr(p, "statusName", "status");
        if (string.IsNullOrEmpty(statusToResolve)) statusToResolve = "On hold";

        int? onHoldStatusId = await entityResolver.ResolveStatusId(statusToResolve);

        await pauseTaskCommand.Execute(new PauseTaskRequest(wpId, onHoldStatusId));
        return $"⏸️ Tarea #{wpId} pausada. El tiempo transcurrido se registró en OpenProject.";
    }
}
