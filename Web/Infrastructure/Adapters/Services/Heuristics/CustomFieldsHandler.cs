using Application.Ports.Services;

namespace Web.Infrastructure.Adapters.Services.Heuristics;

public class CustomFieldsHandler(ICustomFieldService customFieldService) : IHeuristicIntentHandler
{
    public async Task<string?> HandleAsync(string prompt)
    {
        string lower = prompt.ToLowerInvariant().Trim();
        
        // Listar Áreas
        if (lower.Contains("listar áreas") || lower.Contains("mostrar áreas") || (lower.Contains("áreas") && lower.Contains("disponibles")))
        {
            var areas = await customFieldService.ListAreas();
            if (!areas.Any()) return "📁 No se encontraron áreas configuradas.";

            return "📁 **Áreas Disponibles:**\n\n" + 
                   string.Join("\n", areas.Select(a => $"- {a.Value} (ID: {a.Id})"));
        }

        // Listar Módulos
        if (lower.Contains("listar módulos") || lower.Contains("mostrar módulos") || (lower.Contains("módulos") && lower.Contains("disponibles")))
        {
            var modules = await customFieldService.ListModules();
            if (!modules.Any()) return "🧩 No se encontraron módulos configurados.";

            return "🧩 **Módulos Disponibles:**\n\n" + 
                   string.Join("\n", modules.Select(m => $"- {m.Value} (ID: {m.Id})"));
        }

        return null;
    }
}
