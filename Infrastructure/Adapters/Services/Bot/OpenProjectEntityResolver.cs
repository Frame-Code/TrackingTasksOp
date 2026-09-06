using Application.Dto.ListWorkPackages;
using Application.Ports.Auth;
using Application.Ports.Services;
using Application.Ports.UseCases.WorkPackages;
using Domain.Entities.OpenProjectEntities;
using Domain.Entities.OpenProjectEntities.WorkPackage;

namespace Infrastructure.Adapters.Services.Bot;

public class OpenProjectEntityResolver(
    IProjectOpService projectOpService,
    IStatusOpService statusOpService,
    IUserOpService userOpService,
    IListsWorkPackagesCommand listsWorkPackagesCommand,
    IGetWorkPackageCommand getWorkPackageCommand,
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

    public async Task<List<WorkPackage>> FindWorkPackagesBySubject(string subject)
    {
        if (string.IsNullOrWhiteSpace(subject)) return [];

        // Una sola página corta: si el asunto da más de un puñado de coincidencias, la
        // respuesta correcta es preguntar cuál, no traer 200 tareas para elegir.
        var page = await listsWorkPackagesCommand.ExecutePageAsync(
            new ListsWorkPackagesRequest(null, 1, 10, Search: subject, OnlyMine: false));

        // OpenProject busca por asunto O por ID ("subjectOrId"), así que empareja parcial.
        // Un asunto exacto gana: si el usuario escribió el nombre completo, no debería tener
        // que elegir entre esa tarea y las otras que apenas la contienen.
        var exact = page.Items
            .Where(wp => wp.Subject.Equals(subject.Trim(), StringComparison.OrdinalIgnoreCase))
            .ToList();

        return exact.Count > 0 ? exact : page.Items;
    }

    public async Task<int?> GetProjectIdOfWorkPackage(int workPackageId)
    {
        if (workPackageId <= 0) return null;
        var wp = await getWorkPackageCommand.Execute(workPackageId);
        var projectId = wp?.Links.Project.Id ?? 0;
        return projectId > 0 ? projectId : null;
    }
}
