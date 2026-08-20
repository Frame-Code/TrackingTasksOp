using Domain.Entities.TrackingTasksEntities;

namespace Application.Ports.Repositories;

public interface IStatusTaskRepository : IRepository<StatusTask>
{
    /// <summary>Igual criterio que IProjectRepository.GetByIdForInstanceAsync: la PK real es
    /// compuesta (Id, OpenProjectInstanceId).</summary>
    System.Threading.Tasks.Task<StatusTask?> GetByIdForInstanceAsync(int id, int openProjectInstanceId, bool tracking = false);
}