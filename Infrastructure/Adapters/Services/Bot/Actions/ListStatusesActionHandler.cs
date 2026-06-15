using Application.Ports.Services;

namespace Infrastructure.Adapters.Services.Bot.Actions;

public class ListStatusesActionHandler(
    IStatusOpService statusOpService,
    IProjectOpService projectOpService) : IBotActionHandler
{
    public string ActionName => "list_statuses";

    public async Task<string> ExecuteAsync(GroqAction action, int? contextWpId, CancellationToken ct = default)
    {
        string projName = GroqActionParams.GetStr(action.Params, "projectName", "project");
        var allStatuses = await statusOpService.Lists();
        string title = "📋 **Estados disponibles:**";

        if (!string.IsNullOrEmpty(projName))
        {
            var projs = await projectOpService.Lists();
            var proj = projs.FirstOrDefault(pj => pj.Name.Contains(projName, StringComparison.OrdinalIgnoreCase));
            if (proj != null) title = $"📋 **Estados para el proyecto {proj.Name}:**";
        }

        return title + "\n\n" + string.Join("\n", allStatuses.Select(s => $"- **{s.Name}** (ID: {s.Id})"));
    }
}
