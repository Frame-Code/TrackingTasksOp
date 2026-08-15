using Application.Dto.ListWorkPackages;
using Domain.Entities.OpenProjectEntities;
using Domain.Entities.OpenProjectEntities.WorkPackage;

namespace Application.Ports.UseCases.WorkPackages;

public interface IListsWorkPackagesCommand
{
    Task<List<WorkPackage>> Execute(ListsWorkPackagesRequest request);

    /// <summary>
    /// Trae work packages concretos por ID, sin filtrar por asignado ni estado.
    /// El reporte lo usa para resolver asignado y responsable de las tareas con tiempo
    /// registrado, que no vienen en las entradas de tiempo de OpenProject.
    /// </summary>
    Task<List<WorkPackage>> GetByIdsAsync(IReadOnlyCollection<int> ids, CancellationToken ct = default);
}
