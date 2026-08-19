using Application.Dto.ListWorkPackages;
using Domain.Entities.OpenProjectEntities;
using Domain.Entities.OpenProjectEntities.WorkPackage;

namespace Application.Ports.UseCases.WorkPackages;

public interface IListsWorkPackagesCommand
{
    /// <summary>
    /// Trae TODAS las páginas. Lo usan el bot y el reporte, que necesitan el conjunto
    /// completo. Para la pantalla de tareas usar <see cref="ExecutePageAsync"/>: traer
    /// 200 work packages para mostrar 12 cuesta segundos en OpenProject.
    /// </summary>
    Task<List<WorkPackage>> Execute(ListsWorkPackagesRequest request);

    /// <summary>
    /// Trae UNA sola página, con el total para poder paginar. El filtro por estado y la
    /// búsqueda los resuelve OpenProject, no el navegador.
    /// </summary>
    Task<PagedWorkPackages<WorkPackage>> ExecutePageAsync(ListsWorkPackagesRequest request);

    /// <summary>
    /// Trae work packages concretos por ID, sin filtrar por asignado ni estado.
    /// El reporte lo usa para resolver asignado y responsable de las tareas con tiempo
    /// registrado, que no vienen en las entradas de tiempo de OpenProject.
    /// </summary>
    Task<List<WorkPackage>> GetByIdsAsync(IReadOnlyCollection<int> ids, CancellationToken ct = default);
}
