using Domain.Entities.OpenProjectEntities.Activity;

namespace Application.Ports.Services;

public interface IActivityOpService
{
    public Task<List<ActivityAllowedValue>> Lists(int idWorkPackage);
}