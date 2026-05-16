using Domain.Entities.OpenProjectEntities;

namespace Application.Ports.Services;

public interface ICustomFieldService
{
    Task<List<CustomOption>> ListAreas();
    Task<List<CustomOption>> ListModules();
    Task<CustomOption?> FindAreaByName(string name);
    Task<CustomOption?> FindModuleByName(string name);
}
