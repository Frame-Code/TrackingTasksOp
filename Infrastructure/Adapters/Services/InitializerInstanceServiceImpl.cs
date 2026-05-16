using Application.Dto.Auth;
using Application.Ports.Services;
using Domain.Entities.TrackingTasksEntities;
using Infrastructure.DataAccess;
using Infrastructure.Exceptions;
using Infrastructure.Extensions;
using Microsoft.EntityFrameworkCore;
using Task = System.Threading.Tasks.Task;

namespace Infrastructure.Adapters.Services;

public class InitializerInstanceServiceImpl(
    TrackingTasksDbContext db,
    IStatusOpService statusOpService,
    IProjectOpService projectOpService) : IInitializerInstanceService
{
    public async Task InitializeAsync(InitializeInstanceRequest request, CancellationToken ct)
    {
        //Migrate StatusTask
        var statusesOp = await statusOpService.Lists();
        if (statusesOp.Count == 0)
            throw new InitializerInstanceException("No se encontraron estados de tareas en la instancia registrada, intente de nuevo");
        
        var statusLocal = await db.StatusTasks
            .Where(x => x.OpenProjectInstanceId == request.OpenProjectInstanceId)
            .Select(x => x.Name)
            .ToListAsync(ct);

        var statusToSave = statusesOp
            .Where(x => !statusLocal.Contains(x.Name))
            .ToList();
        
        var statusTasks = statusToSave
            .Select(x => new StatusTask
            {
                Id = x.Id,
                IsClosed = x.IsClosed,
                Name = x.Name,
                OpenProjectInstanceId = request.OpenProjectInstanceId
            })
            .ToList();

        //Migrate Projects
        var projects = await projectOpService.Lists();
        if (projects.Count == 0)
            throw new InitializerInstanceException("No se encontraros proyectos en la instancia registrada, intente de nuevo");

        var projectsLocal = await db.Projects
            .Where(x => x.OpenProjectInstanceId == request.OpenProjectInstanceId)
            .Select(x => x.Name)
            .ToListAsync(ct);

        var projectsToSave = projects
            .Where(x => !projectsLocal.Contains(x.Name))
            .ToList();
        
        var projectsTask = projectsToSave
            .Select(x => new Project
            {
                Id = x.Id,
                Name = x.Name,
                Identifier = x.Identifier,
                IsActive = x.IsActive,
                OpenProjectInstanceId = request.OpenProjectInstanceId
            })
            .ToList();
        
        //Create Migration
        var migration = new MigrationData
        {
            Description = $"Tables migrated: {db.GetTableName<StatusTask>()}, {db.GetTableName<Project>()}. User owner: id:{request.UserId}, username:{request.Username}",
            Name = "TrackingTasksOp Migration Data"
        };

        await db.StatusTasks.AddRangeAsync(statusTasks, ct);
        await db.Projects.AddRangeAsync(projectsTask, ct);
        await db.MigrationsData.AddAsync(migration, ct);
        await db.SaveChangesAsync(ct);
    }
}