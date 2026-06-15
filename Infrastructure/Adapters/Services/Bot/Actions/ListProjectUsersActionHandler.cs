using Application.Ports.Services;

namespace Infrastructure.Adapters.Services.Bot.Actions;

public class ListProjectUsersActionHandler(
    IUserOpService userOpService,
    IOpenProjectEntityResolver entityResolver) : IBotActionHandler
{
    public string ActionName => "list_project_users";

    public async Task<string> ExecuteAsync(GroqAction action, int? contextWpId, CancellationToken ct = default)
    {
        var p = action.Params;
        string pName = GroqActionParams.GetStr(p, "projectName", "project");
        if (string.IsNullOrEmpty(pName)) throw new Exception("El parámetro 'projectName' (nombre del proyecto) es obligatorio.");

        var projectId = await entityResolver.ResolveProjectId(pName);
        if (!projectId.HasValue) throw new Exception($"No pude encontrar el proyecto '{pName}'. Por favor, verifica el nombre o usa 'listar proyectos'.");

        var users = await userOpService.ListAssignees(projectId.Value);
        if (!users.Any()) return $"No se encontraron usuarios asignables en el proyecto '{pName}'.";

        return $"👥 **Usuarios del proyecto '{pName}':**\n\n" +
               string.Join("\n", users.Select(u => $"- **{u.Name}**"));
    }
}
