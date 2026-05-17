using Application.Dto.WorkPackages;
using Domain.Entities.OpenProjectEntities.WorkPackage;

namespace Application.Ports.UseCases.WorkPackages;

public interface ICreateWorkPackageCommand
{
    Task<WorkPackage> Execute(CreateWorkPackageRequest request);
}
