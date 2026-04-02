using Domain.Entities.OpenProjectEntities;
using Domain.Entities.OpenProjectEntities.Activity;
using Domain.Entities.OpenProjectEntities.Status;

namespace Application.Ports.Services;

public interface IStatusOpService
{
    Task<List<Status>> Lists();
}