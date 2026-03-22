using Microsoft.EntityFrameworkCore;
using Web.Infrastructure.DataAccess;

namespace Web.Infrastructure.Config.Extensions;

public static class InitDbContextExtensions
{
    public static IServiceCollection AddDbContext(this IServiceCollection services, IConfiguration configuration)
    {
        var trackingTasksConnectionString = configuration.GetConnectionString("TrackingTasks")
            ?? throw new ArgumentException("ConnectionString tracking tasks is not set");

        services.AddDbContext<TrackingTasksDbContext>(options =>
        {
            options.UseSqlServer(trackingTasksConnectionString, optionsBuilder =>
            {
                optionsBuilder.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
                optionsBuilder.EnableRetryOnFailure();
            });
            options.EnableSensitiveDataLogging();
            options.EnableDetailedErrors();
            options.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
        });
        
        return services;
    }
}

public static class DbContextExtensions
{
    public static string GetTableName<T>(this DbContext context) where T : class
    {
        var entityType = context.Model.FindEntityType(typeof(T));
        return entityType?.GetTableName() 
            ?? throw new InvalidOperationException($"{typeof(T).Name} no está registrada en el modelo.");
    }

    public static string GetFullTableName<T>(this DbContext context) where T : class
    {
        var entityType = context.Model.FindEntityType(typeof(T));
        var table = entityType?.GetTableName();
        var schema = entityType?.GetSchema();
        
        return schema != null ? $"{schema}.{table}" : table
            ?? throw new InvalidOperationException($"{typeof(T).Name} no está registrada en el modelo.");
    }
    
    public static async Task<T> AddOrUpdateAsync<T>(this DbContext context, T entity, int id) where T : class
    {
        var existing = await context.Set<T>().FindAsync(id);

        if (existing is null)
            await context.Set<T>().AddAsync(entity);
        else
            context.Update(entity);

        await context.SaveChangesAsync();
        return entity;
    }
}
