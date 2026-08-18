using Application.Dto.Conversation;
using Application.Dto.ListWorkPackages;
using Application.Ports.Services;
using Application.Ports.UseCases.WorkPackages;

namespace Infrastructure.Adapters.Services.Bot.Actions;

public class ListTasksActionHandler(
    IListsWorkPackagesCommand listsWorkPackagesCommand,
    IStatusOpService statusOpService,
    IOpenProjectEntityResolver entityResolver) : IBotActionHandler
{
    public string ActionName => "list_tasks";

    public async Task<string> ExecuteAsync(GroqAction action, int? contextWpId, ConversationContext? conversationContext = null, CancellationToken ct = default)
    {
        string sName = GroqActionParams.GetStr(action.Params, "statusName", "status");

        if (!string.IsNullOrEmpty(sName))
        {
            var statusId = await entityResolver.ResolveStatusId(sName);
            if (statusId == null)
            {
                var allStatuses = await statusOpService.Lists();
                string available = string.Join(", ", allStatuses.Select(s => s.Name));
                return $"⚠️ No reconozco el estado '{sName}'. Estados disponibles: {available}.";
            }

            var filteredWps = await listsWorkPackagesCommand.Execute(new ListsWorkPackagesRequest(null, 0, 50, statusId));
            if (!filteredWps.Any()) return $"✅ No tienes tareas en estado '{sName}'.";

            return WorkPackageFormatter.FormatGroupedByProject(filteredWps, $"📝 **Tareas en estado '{sName}':**");
        }

        // "Tareas pendientes" = abiertas. El listado general ya incluye las cerradas.
        var wps = await listsWorkPackagesCommand.Execute(new ListsWorkPackagesRequest(null, 0, 50, OnlyOpen: true));
        if (!wps.Any()) return "✅ No tienes tareas pendientes.";

        return WorkPackageFormatter.FormatGroupedByProject(wps);
    }
}
