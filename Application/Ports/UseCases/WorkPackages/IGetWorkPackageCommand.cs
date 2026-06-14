using Domain.Entities.OpenProjectEntities.WorkPackage;

namespace Application.Ports.UseCases.WorkPackages;

public interface IGetWorkPackageCommand
{
    Task<WorkPackage?> Execute(int workPackageId);
}
