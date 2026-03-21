using System.Linq.Expressions;
using Application.Ports.Repositories;
using Microsoft.EntityFrameworkCore;
using Web.Infrastructure.DataAccess;
using Task = Domain.Entities.TrackingTasksEntities.Task;

namespace Web.Infrastructure.Adapters.Repositories;

public class TaskRepositoryImpl(TrackingTasksDbContext context) : ITaskRepository
{
    public async Task<IEnumerable<Task>> GetAllAsync(Expression<Func<Task, bool>>? filter, bool tracking = false)
    {
        var query = tracking ? context.Tasks.AsQueryable() : context.Tasks.AsNoTracking().AsQueryable();
        return filter is null? await query.ToListAsync() 
            : await query.Where(filter).ToListAsync();
    }

    public async Task<Task?> GetByIdAsync(int id, bool tracking = false)
    {
        var query = tracking? context.Tasks.AsQueryable() : context.Tasks.AsNoTracking().AsQueryable();
        return await query.FirstOrDefaultAsync(x => x.OpenProjectId == id);
    }

    public async Task<Task> SaveAsync(Task task)
    {
        var taskSaved = await context.Tasks.AddAsync(task);
        await context.SaveChangesAsync();
        return taskSaved.Entity;
    }

    public async System.Threading.Tasks.Task SaveAllAsync(IEnumerable<Task> tasks)
    {
        await context.AddRangeAsync(tasks);
        await context.SaveChangesAsync();
    }
}