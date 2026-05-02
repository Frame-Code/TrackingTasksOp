using System.Linq.Expressions;
using Application.Ports.Repositories;
using Infrastructure.DataAccess;
using Infrastructure.Extensions;
using Microsoft.EntityFrameworkCore;
using Task = Domain.Entities.TrackingTasksEntities.Task;

namespace Infrastructure.Adapters.Repositories;

public class TaskRepositoryImpl(TrackingTasksDbContext context) : ITaskRepository
{
    public async Task<IEnumerable<Task>> GetAllAsync(Expression<Func<Task, bool>>? filter, bool tracking = false)
    {
        var query = tracking 
            ? context.Tasks.AsQueryable() 
            : context.Tasks.AsNoTracking().AsQueryable();
        
        if (filter is not null)
            query = query.Where(filter);

        return await query
            .Include(x => x.TasksTimeDetails)
            .ToListAsync();
    }

    public async Task<Task?> GetByIdAsync(int id, bool tracking = false)
    {
        var query = tracking
            ? context.Tasks.AsQueryable() 
            : context.Tasks.AsNoTracking().AsQueryable();
        
        return await query
            .Include(x => x.TasksTimeDetails)
            .FirstOrDefaultAsync(x => x.WorkPackageId == id);
    }

    public async Task<Task> SaveAsync(Task entity)
    {
        return await context.AddOrUpdateAsync(entity, entity.WorkPackageId);
    }

    public async System.Threading.Tasks.Task SaveAllAsync(IEnumerable<Task> tasks)
    {
        await context.AddRangeAsync(tasks);
        await context.SaveChangesAsync();
    }
}