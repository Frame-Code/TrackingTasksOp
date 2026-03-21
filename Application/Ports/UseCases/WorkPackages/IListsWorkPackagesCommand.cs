using Application.Dto.ListWorkPackages;
using Domain.Entities.OpenProjectEntities;

namespace Application.Ports.UseCases.WorkPackages;

public interface IListsWorkPackagesCommand
{
    Task<List<WorkPackage>> Execute(ListsWorkPackagesRequest request);
}
