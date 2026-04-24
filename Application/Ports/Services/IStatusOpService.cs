using Domain.Entities.OpenProjectEntities.Status;

namespace Application.Ports.Services;

public interface IStatusOpService
{
    Task<List<Status>> Lists();
    Task<Status?> FindByNameAsync(string name);
}