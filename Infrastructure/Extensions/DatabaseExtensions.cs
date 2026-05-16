using Application.Ports.Services;
using Domain.Entities.TrackingTasksEntities;
using Infrastructure.DataAccess;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.Extensions;

public static class DatabaseExtensions
{
    public static async System.Threading.Tasks.Task MigrateAsync(this IServiceProvider services)
    {
        //Migrate Schema
        using var scope = services.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<TrackingTasksDbContext>();
        await db.Database.MigrateAsync();
    } 
}