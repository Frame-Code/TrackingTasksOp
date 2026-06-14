using Application.Ports.Services;

namespace Infrastructure.Adapters.Services.Bot;

public class OpenProjectEntityResolver(
    IProjectOpService projectOpService,
    IStatusOpService statusOpService,
    IUserOpService userOpService) : IOpenProjectEntityResolver
{
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
        var user = projectId.HasValue
            ? await userOpService.FindAssigneeByName(projectId.Value, name)
            : await userOpService.FindByName(name);
        return user?.Id;
    }
}
