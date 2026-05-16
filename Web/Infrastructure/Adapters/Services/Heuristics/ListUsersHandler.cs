using Application.Ports.Services;

namespace Web.Infrastructure.Adapters.Services.Heuristics;

public class ListUsersHandler(IUserOpService userOpService) : IHeuristicIntentHandler
{
    public async Task<string?> HandleAsync(string prompt)
    {
        string lower = prompt.ToLowerInvariant().Trim();
        if (lower.Contains("usuarios") || lower.Contains("listar usuarios"))
        {
            try 
            {
                var users = await userOpService.Lists();
                if (!users.Any()) return "👥 No se encontraron usuarios.";

                return "👥 **Usuarios Registrados:**\n\n" + 
                       string.Join("\n", users.Select(u => $"- {u.Name} (ID: {u.Id})"));
            }
            catch (UnauthorizedAccessException)
            {
                return "⚠️ **Acceso Denegado:** No tienes permisos suficientes en OpenProject para listar los usuarios del sistema. Contacta con tu administrador para que te asigne el permiso 'Ver usuarios' o usa una API Key con privilegios de administrador.";
            }
            catch (Exception ex)
            {
                return $"❌ **Error al listar usuarios:** {ex.Message}";
            }
        }
        return null;
    }
}
