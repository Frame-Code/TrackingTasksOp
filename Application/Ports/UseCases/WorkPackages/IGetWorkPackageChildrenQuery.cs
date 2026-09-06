using Domain.Entities.OpenProjectEntities.WorkPackage;

namespace Application.Ports.UseCases.WorkPackages;

/// <summary>
/// Hijos directos de un work package, para armar el árbol nivel por nivel.
/// </summary>
public interface IGetWorkPackageChildrenQuery
{
    /// <summary>
    /// A diferencia del listado de tareas, NO filtra por asignado: el árbol muestra la
    /// jerarquía completa, sea de quien sea cada hijo. "Completa" significa igual dentro de
    /// los permisos de quien consulta — el filtro lo aplica OpenProject con su API key.
    /// </summary>
    Task<List<WorkPackage>> ExecuteAsync(int parentId, CancellationToken ct = default);
}
