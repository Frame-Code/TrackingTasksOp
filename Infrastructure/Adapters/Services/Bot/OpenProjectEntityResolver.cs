using Application.Ports.Auth;
using Application.Ports.Services;

namespace Infrastructure.Adapters.Services.Bot;

public class OpenProjectEntityResolver(
    IProjectOpService projectOpService,
    IStatusOpService statusOpService,
    IUserOpService userOpService,
    CurrentUser currentUser) : IOpenProjectEntityResolver
{
    /// <summary>
    /// Formas en que un usuario puede referirse a sí mismo al pedir que se le asigne una tarea
    /// (ej. "asígnamela a mí"). No existe ningún usuario de OpenProject con estos nombres, por lo
    /// que deben resolverse contra el OpenProjectUserId de la cuenta logueada en vez de buscarse.
    /// </summary>
    private static readonly HashSet<string> SelfReferenceTerms = new(StringComparer.OrdinalIgnoreCase)
    {
        "yo", "mi", "mí", "a mi", "a mí", "yo mismo", "yo misma", "conmigo",
        "mi cuenta", "mi usuario", "myself", "me", "i", "my account"
    };
    public async Task<int?> ResolveProjectId(string name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        var projects = await projectOpService.Lists();
        var matched = projects.FirstOrDefault(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase) || p.Identifier.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (matched != null) return matched.Id;

        return projects.FirstOrDefault(p => p.Name.Contains(name, StringComparison.OrdinalIgnoreCase) || p.Identifier.Contains(name, StringComparison.OrdinalIgnoreCase))?.Id;
    }

    public async Task<int?> ResolveStatusId(string name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        var statuses = await statusOpService.Lists();
        var matched = statuses.FirstOrDefault(s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (matched != null) return matched.Id;

        return statuses.FirstOrDefault(s => s.Name.Contains(name, StringComparison.OrdinalIgnoreCase))?.Id;
    }

    public async Task<int?> ResolveUserId(string name, int? projectId = null)
    {
        if (string.IsNullOrEmpty(name)) return null;

        if (SelfReferenceTerms.Contains(name.Trim()))
            return currentUser.OpenProjectUserId;

        var user = projectId.HasValue
            ? await userOpService.FindAssigneeByName(projectId.Value, name)
            : await userOpService.FindByName(name);
        return user?.Id;
    }
}
