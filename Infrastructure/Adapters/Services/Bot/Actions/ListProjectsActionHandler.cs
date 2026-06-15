using Application.Ports.Services;

namespace Infrastructure.Adapters.Services.Bot.Actions;

public class ListProjectsActionHandler(IProjectOpService projectOpService) : IBotActionHandler
{
    public string ActionName => "list_projects";

    public async Task<string> ExecuteAsync(GroqAction action, int? contextWpId, CancellationToken ct = default)
    {
        var projects = await projectOpService.Lists();
        if (!projects.Any()) return "No se encontraron proyectos disponibles.";
        return "📋 **Tus Proyectos Disponibles:**\n\n" +
               string.Join("\n", projects.Select(proj => $"- **{proj.Name}** (ID: {proj.Id})"));
    }
}
