using Domain.Entities.OpenProjectEntities;
using Domain.Entities.OpenProjectEntities.Project;

namespace Application.Ports.Services;

public interface IProjectOpService
{
    Task<List<Project>> Lists();
    Task<Project?> FindByName(string name);
}