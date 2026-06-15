namespace Infrastructure.Adapters.Services.Bot;

/// <summary>
/// Resuelve nombres de entidades (proyectos, estados, usuarios) a sus IDs en OpenProject.
/// </summary>
public interface IOpenProjectEntityResolver
{
    Task<int?> ResolveProjectId(string name);
    Task<int?> ResolveStatusId(string name);

    /// <summary>
    /// Resuelve el nombre de un usuario a su ID. Si se indica <paramref name="projectId"/>,
    /// la búsqueda se restringe a los miembros asignables de ese proyecto
    /// (endpoint /api/v3/projects/{id}/available_assignees, accesible para usuarios no administradores).
    /// Sin <paramref name="projectId"/> se usa el endpoint global /api/v3/users (requiere permisos de administrador).
    /// </summary>
    Task<int?> ResolveUserId(string name, int? projectId = null);
}
