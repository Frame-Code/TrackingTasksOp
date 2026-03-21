using Domain.Entities.OpenProjectEntities;

namespace Application.Ports.Services;

public interface IStatusOpService
{
    Task<List<Status>> Lists();
}