using Application.Dto.Conversation;
using Application.Ports.UseCases.WorkPackages;
using Infrastructure.Adapters.UseCases.WorkPackages;

namespace Infrastructure.Adapters.Services.Bot.Actions;

public class UpdateTaskDatesActionHandler(
    IUpdateWorkPackageCommand updateWorkPackageCommand) : IBotActionHandler
{
    public string ActionName => "update_task_dates";

    public async Task<string> ExecuteAsync(GroqAction action, int? contextWpId, ConversationContext? conversationContext = null, CancellationToken ct = default)
    {
        var p = action.Params;
        int wpId = GroqActionParams.GetInt(p, "workPackageId", "id", "wpId");
        if (wpId == 0 && contextWpId.HasValue) wpId = contextWpId.Value;
        if (wpId <= 0) throw new Exception("Se requiere un ID de tarea válido (workPackageId).");

        DateOnly? startDate = GroqActionParams.GetDate(p, "startDate");
        DateOnly? dueDate = GroqActionParams.GetDate(p, "dueDate");

        if (!startDate.HasValue && !dueDate.HasValue)
            throw new Exception("Se requiere al menos una fecha (startDate o dueDate) en formato yyyy-MM-dd.");

        await updateWorkPackageCommand.Execute(
            wpId,
            startDate: startDate?.ToString("yyyy-MM-dd") ?? UpdateWorkPackageCommandImpl.NoChange,
            dueDate: dueDate?.ToString("yyyy-MM-dd") ?? UpdateWorkPackageCommandImpl.NoChange);

        var changes = new List<string>();
        if (startDate.HasValue) changes.Add($"fecha de inicio: **{startDate.Value:yyyy-MM-dd}**");
        if (dueDate.HasValue) changes.Add($"fecha de fin: **{dueDate.Value:yyyy-MM-dd}**");

        return $"📅 Tarea #{wpId} actualizada con {string.Join(" y ", changes)}.";
    }
}
