using Domain.Entities.OpenProjectEntities.Activity;

namespace Application.Ports.Services;

public interface IActivityOpService
{
    Task<List<ActivityAllowedValue>> Lists(int idWorkPackage);
    Task<ActivityAllowedValue?> FindByNameAsync(string name, int workPackageId);
}