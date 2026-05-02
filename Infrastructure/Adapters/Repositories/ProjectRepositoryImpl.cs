using System.Linq.Expressions;
using Application.Ports.Repositories;
using Domain.Entities.TrackingTasksEntities;
using Infrastructure.DataAccess;
using Infrastructure.Extensions;
using Microsoft.EntityFrameworkCore;
using Task = System.Threading.Tasks.Task;

namespace Infrastructure.Adapters.Repositories;

public class ProjectRepositoryImpl(TrackingTasksDbContext context) : IProjectRepository
{
    public async Task<IEnumerable<Project>> GetAllAsync(Expression<Func<Project, bool>>? filter, bool tracking = false)
    {
        var query = tracking ? context.Projects.AsQueryable() : context.Projects.AsNoTracking().AsQueryable();
        return filter is null? await query.ToListAsync() 
            : await query.Where(filter).ToListAsync();
    }

    public async Task<Project?> GetByIdAsync(int id, bool tracking = false)
    {
        var query = tracking? context.Projects.AsQueryable() : context.Projects.AsNoTracking().AsQueryable();
        return await query.FirstOrDefaultAsync(x => x.Id == id);
    }

    public async Task<Project> SaveAsync(Project entity)
    {
        return await context.AddOrUpdateAsync(entity, entity.Id);
    }

    public async Task SaveAllAsync(IEnumerable<Project> entities)
    {
        await context.AddRangeAsync(entities);
        await context.SaveChangesAsync();
    }
}