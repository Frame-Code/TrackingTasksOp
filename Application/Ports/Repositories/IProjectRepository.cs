using Domain.Entities.TrackingTasksEntities;

namespace Application.Ports.Repositories;

public interface IProjectRepository : IRepository<Project>
{
    /// <summary>
    /// Busca el proyecto por Id, acotado a la instancia de OpenProject. La PK real es
    /// compuesta (Id, OpenProjectInstanceId): buscar solo por Id (GetByIdAsync heredado)
    /// puede devolver el proyecto de OTRO tenant si dos instancias distintas tienen un
    /// proyecto con el mismo Id numérico (algo casi seguro, ya que los IDs de OpenProject
    /// son secuenciales por instancia).
    /// </summary>
    System.Threading.Tasks.Task<Project?> GetByIdForInstanceAsync(int id, int openProjectInstanceId, bool tracking = false);
}