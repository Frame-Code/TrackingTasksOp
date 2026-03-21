using Application.Ports.Repositories;
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
        await db.Database.MigrateAsync();
        
        //--------------------------------Migrate Data
        var statusOpService = scope.ServiceProvider.GetRequiredService<IStatusOpService>();
        var statusTaskRepository = scope.ServiceProvider.GetRequiredService<IStatusTaskRepository>();
        
        //Migrate StatusTask
        var statuses = await statusOpService.Lists();
        if(statuses.Count == 0) 
            throw new Exception("No statuses found");

        var statusTasks = statuses
            .Select(x => new StatusTask
            {
                Id = x.Id,
                IsClosed = x.IsClosed,
                Name = x.Name
            })
            .ToList();
        await statusTaskRepository.SaveAllAsync(statusTasks);
            
        //Migrate Projects
        
        return app;
    } 
}