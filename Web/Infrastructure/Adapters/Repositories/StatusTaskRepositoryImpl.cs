using System.Linq.Expressions;
using Application.Ports.Repositories;
using Domain.Entities.TrackingTasksEntities;
using Task = System.Threading.Tasks.Task;

namespace Web.Infrastructure.Adapters.Repositories;

public class StatusTaskRepositoryImpl : IStatusTaskRepository
{
    public Task<IEnumerable<StatusTask>> GetAllAsync(Expression<Func<StatusTask, bool>>? filter, bool tracking = false)
    {
        throw new NotImplementedException();
    }

    public Task<StatusTask?> GetByIdAsync(int id, bool tracking = false)
    {
        throw new NotImplementedException();
    }

    public Task<StatusTask> SaveAsync(StatusTask task)
    {
        throw new NotImplementedException();
    }

    public Task SaveAllAsync(IEnumerable<StatusTask> tasks)
    {
        throw new NotImplementedException();
    }
}