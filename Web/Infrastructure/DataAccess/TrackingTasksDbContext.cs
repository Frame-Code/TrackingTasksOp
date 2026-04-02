using Domain.Entities.TrackingTasksEntities;
using Microsoft.EntityFrameworkCore;
using Task = Domain.Entities.TrackingTasksEntities.Task;

namespace Web.Infrastructure.DataAccess;

public class TrackingTasksDbContext(DbContextOptions<TrackingTasksDbContext> options) : DbContext(options)
{
    public DbSet<Project>  Projects { get; set; } = null!;
    public DbSet<Task> Tasks { get; set; } = null!;
    public DbSet<TaskTimeDetail>  TasksTimeDetails { get; set; } = null!;
    public DbSet<StatusTask>  StatusTasks { get; set; } = null!;
    public DbSet<MigrationData>  MigrationsData { get; set; } = null!;
      protected override void OnModelCreating(ModelBuilder modelBuilder)
      {
          modelBuilder.ApplyConfigurationsFromAssembly(typeof(TrackingTasksDbContext).Assembly);
      }
}