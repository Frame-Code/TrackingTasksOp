using System.Linq.Expressions;
using Application.Ports.Repositories;
using Domain.Entities.TrackingTasksEntities;
using Infrastructure.DataAccess;
using Infrastructure.Extensions;
using Microsoft.EntityFrameworkCore;
using Task = System.Threading.Tasks.Task;

namespace Infrastructure.Adapters.Repositories;

public class StatusTaskRepositoryImpl(TrackingTasksDbContext context) : IStatusTaskRepository
{
    public async  Task<IEnumerable<StatusTask>> GetAllAsync(Expression<Func<StatusTask, bool>>? filter, bool tracking = false)
    {
        var query = tracking ? context.StatusTasks.AsQueryable() : context.StatusTasks.AsNoTracking().AsQueryable();
        return filter is null? await query.ToListAsync() 
            : await query.Where(filter).ToListAsync();
    }

    public async Task<StatusTask?> GetByIdAsync(int id, bool tracking = false)
    {
        var query = tracking? context.StatusTasks.AsQueryable() : context.StatusTasks.AsNoTracking().AsQueryable();
        return await query.FirstOrDefaultAsync(x => x.Id == id);
    }

    public async Task<StatusTask> SaveAsync(StatusTask entity)
    {
        return await context.AddOrUpdateAsync(entity, entity.Id);
    }

    public async Task SaveAllAsync(IEnumerable<StatusTask> entities)
    {
        await context.AddRangeAsync(entities);
        await context.SaveChangesAsync();
    }
}