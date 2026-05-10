using Domain.Entities.TrackingTasksEntities;
using Infrastructure.DataAccess.Entities;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Task = Domain.Entities.TrackingTasksEntities.Task;

namespace Infrastructure.DataAccess;

public class TrackingTasksDbContext(DbContextOptions<TrackingTasksDbContext> options)
    : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<Project> Projects { get; set; } = null!;
    public DbSet<Task> Tasks { get; set; } = null!;
    public DbSet<TaskTimeDetail> TasksTimeDetails { get; set; } = null!;
    public DbSet<StatusTask> StatusTasks { get; set; } = null!;
    public DbSet<MigrationData> MigrationsData { get; set; } = null!;
    public DbSet<OpenProjectInstance> OpenProjectInstances { get; set; } = null!;
    public DbSet<UserCredential> UserCredentials { get; set; } = null!;
    public DbSet<LocalCredential> LocalCredentials { get; set; } = null!;
    public DbSet<OAuthCredential> OAuthCredentials { get; set; } = null!;
    public DbSet<AuthAuditLog> AuthAuditLogs { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TrackingTasksDbContext).Assembly);
    }
}
