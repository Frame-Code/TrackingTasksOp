using Application.Ports.Services;
using Domain.Entities.TrackingTasksEntities;
using Infrastructure.DataAccess;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.Extensions;

public static class DatabaseExtensions
{
    public static async System.Threading.Tasks.Task InitializeDatabaseAsync(this IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var sp = scope.ServiceProvider;

        //--------------------------------Migrate Schema
        var db = sp.GetRequiredService<TrackingTasksDbContext>();
        await db.Database.MigrateAsync();

        //--------------------------------Migrate Data
        var any = await db.MigrationsData.AnyAsync();
        if (any)
            return;

        var statusOpService = sp.GetRequiredService<IStatusOpService>();
        var projectOpService = sp.GetRequiredService<IProjectOpService>();

        //Migrate StatusTask
        var statuses = await statusOpService.Lists();
        if (statuses.Count == 0)
            throw new Exception("No statuses found");

        var statusTasks = statuses
            .Select(x => new StatusTask
            {
                Id = x.Id,
                IsClosed = x.IsClosed,
                Name = x.Name
            })
            .ToList();

        //Migrate Projects
        var projects = await projectOpService.Lists();
        if (projects.Count == 0)
            throw new Exception("No projects found");

        var projectsTask = projects
            .Select(x => new Project
            {
                Id = x.Id,
                Name = x.Name,
                Identifier = x.Identifier,
                IsActive = x.IsActive
            })
            .ToList();

        //Create Migration
        var migration = new MigrationData
        {
            Description = $"Tables migrated: {db.GetTableName<StatusTask>()}, {db.GetTableName<Project>()}",
            Name = "TrackingTasksOp Migration Data"
        };

        await db.StatusTasks.AddRangeAsync(statusTasks);
        await db.Projects.AddRangeAsync(projectsTask);
        await db.MigrationsData.AddAsync(migration);
        await db.SaveChangesAsync();
    } 
}