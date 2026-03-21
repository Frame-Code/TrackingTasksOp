using Microsoft.EntityFrameworkCore;
using Web.Infrastructure.DataAccess;

namespace Web.Infrastructure.Config.Extensions;

public static class DbContextExtensions
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