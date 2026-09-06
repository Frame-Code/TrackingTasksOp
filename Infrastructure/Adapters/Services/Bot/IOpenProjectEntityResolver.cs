using Domain.Entities.OpenProjectEntities.WorkPackage;

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

    /// <summary>
    /// Busca work packages por asunto para resolver a qué padre se refiere el usuario
    /// ("una subtarea de Levantamiento de datos"). NO filtra por asignado: el padre suele ser
    /// de otra persona. Devuelve todas las coincidencias para que quien llama decida si usar
    /// la única que hay o preguntar cuál de varias.
    /// </summary>
    Task<List<WorkPackage>> FindWorkPackagesBySubject(string subject);

    /// <summary>Proyecto al que pertenece un work package. Null si no existe o no se puede ver.</summary>
    Task<int?> GetProjectIdOfWorkPackage(int workPackageId);
}
