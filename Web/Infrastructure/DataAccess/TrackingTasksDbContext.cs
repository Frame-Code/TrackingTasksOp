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
      protected override void OnModelCreating(ModelBuilder modelBuilder)
      {
          // ── Project ──────────────────────────────────────────────
          modelBuilder.Entity<Project>(entity =>
          {
              entity.ToTable("Projects");
              entity.HasKey(p => p.Id);
              entity.Property(p => p.Name)
                    .IsRequired()
                    .HasMaxLength(200);
              entity.Property(p => p.Code)
                    .IsRequired()
                    .HasMaxLength(50);
              entity.Property(p => p.Description)
                    .HasMaxLength(500);
          });

          // ── Task ─────────────────────────────────────────────────
          modelBuilder.Entity<Task>(entity =>
          {
              entity.ToTable("Tasks");
              entity.HasKey(t => t.OpenProjectId);
              entity.Property(t => t.OpenProjectId)
                    .IsRequired()
                    .ValueGeneratedNever();
              entity.Property(t => t.Name)
                    .IsRequired()
                    .HasMaxLength(200);
              entity.Property(t => t.Description)
                    .HasMaxLength(500);
              entity.Property(t => t.CreatedAt)
                    .HasColumnType("datetime");
              
              entity.HasOne(t => t.Project)
                    .WithMany()
                    .HasForeignKey(t => t.ProjectId)
                    .OnDelete(DeleteBehavior.Restrict);
              
              entity.HasOne(t => t.StatusTask)
                    .WithMany()
                    .HasForeignKey(t => t.StatusTaskId)
                    .OnDelete(DeleteBehavior.Restrict);
              
              entity.Ignore(t => t.GetTotalHoursWorked());
          });

          // ── TaskTimeDetail ────────────────────────────────────────
          modelBuilder.Entity<TaskTimeDetail>(entity =>
          {
              entity.ToTable("TaskTimeDetails");
              entity.HasKey(t => t.Id);
              entity.Property(t => t.StartTime)
                    .IsRequired()
                    .HasColumnType("datetime");
              entity.Property(t => t.EndTime)
                    .HasColumnType("datetime");
              
              entity.HasOne(t => t.Task)
                    .WithMany(t => t.TasksTimeDetails)
                    .HasForeignKey(t => t.IdTask)
                    .OnDelete(DeleteBehavior.Cascade);
              
              entity.Ignore(t => t.GetHoursWorked());
          });
          
          // ── StatusTasks ────────────────────────────────────────
          modelBuilder.Entity<StatusTask>(entity =>
          {
                entity.ToTable("StatusTasks");
                entity.HasKey(t => t.Id);
                entity.Property(t => t.Id)
                      .IsRequired()
                      .ValueGeneratedNever();
                entity.Property(t => t.Name)
                      .IsRequired()
                      .HasMaxLength(200);
                entity.Property(t => t.IsClosed)
                      .IsRequired();
          });
      }
}