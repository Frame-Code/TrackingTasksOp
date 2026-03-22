using System.Linq.Expressions;
using Application.Ports.Repositories;
using Domain.Entities.TrackingTasksEntities;
using Microsoft.EntityFrameworkCore;
using Web.Infrastructure.DataAccess;
using Task = System.Threading.Tasks.Task;

namespace Web.Infrastructure.Adapters.Repositories;

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
        var statusSaved = await context.Projects.AddAsync(entity);
        await context.SaveChangesAsync();
        return statusSaved.Entity;
    }

    public async Task SaveAllAsync(IEnumerable<Project> entities)
    {
        await context.AddRangeAsync(entities);
        await context.SaveChangesAsync();
    }
}