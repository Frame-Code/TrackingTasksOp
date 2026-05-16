using Application.Ports.Services;

namespace Web.Infrastructure.Adapters.Services.Heuristics;

public class ListProjectsHandler(IProjectOpService projectOpService) : IHeuristicIntentHandler
{
    public async Task<string?> HandleAsync(string prompt)
    {
        string lower = prompt.ToLowerInvariant().Trim();
        if (lower == "proyectos" || lower.Contains("listar proyectos") || lower == "mis proyectos")
        {
            var projects = await projectOpService.Lists();
            if (!projects.Any()) return "📂 No hay proyectos disponibles.";
            
            return "📋 **Tus Proyectos Disponibles:**\n\n" + 
                   string.Join("\n", projects.Select(p => $"- **{p.Name}** (ID: {p.Id})"));
        }
        return null;
    }
}
