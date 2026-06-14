using Domain.Entities.OpenProjectEntities.User;

namespace Application.Ports.Services;

public interface IUserOpService
{
    Task<List<User>> Lists();
    Task<User?> FindByName(string name);
    Task<List<User>> ListAssignees(int projectId);
    Task<User?> FindAssigneeByName(int projectId, string name);
}
