using Application.Ports.Services;

namespace Web.Infrastructure.Adapters.Services.Heuristics;

public class ListStatusesHandler(IStatusOpService statusOpService) : IHeuristicIntentHandler
{
    public async Task<string?> HandleAsync(string prompt)
    {
        string lower = prompt.ToLowerInvariant().Trim();
        if (lower.Contains("estados") || lower.Contains("listar estados"))
        {
            var states = await statusOpService.Lists();
            if (!states.Any()) return "🗂️ No se encontraron estados configurados.";

            return "🗂️ **Estados Disponibles:**\n\n" + 
                   string.Join("\n", states.Select(u => $"- {u.Name} (ID: {u.Id})"));
        }
        return null;
    }
}
