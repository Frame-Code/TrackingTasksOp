using Application.Ports.Services;
using Domain.Entities.TrackingTasksEntities;
using Microsoft.EntityFrameworkCore;
using Web.Infrastructure.DataAccess;

namespace Web.Infrastructure.Config.Extensions;

public static class MigrationsExtensions
{
    public static async Task<WebApplication> ConfigurateDbAsync(this WebApplication app)
    {
        //--------------------------------Migrate Schema
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TrackingTasksDbContext>();
        
        if (app.Environment.IsDevelopment()) 
            await db.Database.MigrateAsync();
        
        //--------------------------------Migrate Data
        var any = db.MigrationsData.Any();
        if(any)
            return app;
        
        var statusOpService = scope.ServiceProvider.GetRequiredService<IStatusOpService>();
        var projectOpService = scope.ServiceProvider.GetRequiredService<IProjectOpService>();

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
        
        return app;
    } 
}