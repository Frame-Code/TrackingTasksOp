using Domain.Entities.OpenProjectEntities;

namespace Application.Ports.Services;

public interface IProjectOpService
{
    Task<List<Project>> Lists();
}