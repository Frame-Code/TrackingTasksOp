using Infrastructure.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.DataAccess.Configurations;

public class TaskConfiguration : IEntityTypeConfiguration<Domain.Entities.TrackingTasksEntities.Task>
{
    public void Configure(EntityTypeBuilder<Domain.Entities.TrackingTasksEntities.Task> builder)
    {
        builder.ToTable("Tasks");

        builder.HasKey(t => new { t.UserId, t.WorkPackageId });

        builder.Property(t => t.UserId)
            .IsRequired()
            .HasMaxLength(450);

        builder.Property(t => t.WorkPackageId)
            .IsRequired()
            .ValueGeneratedNever();

        builder.Property(t => t.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(t => t.Description)
            .HasMaxLength(500);

        builder.Property(t => t.CreatedAt)
            .HasColumnType("datetime");

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(t => t.OpenProjectInstance)
            .WithMany()
            .HasForeignKey(t => t.OpenProjectInstanceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(t => t.Project)
            .WithMany()
            .HasForeignKey(t => new {t.ProjectId, t.OpenProjectInstanceId})
            .HasPrincipalKey(p => new {p.Id, p.OpenProjectInstanceId})
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(t => t.StatusTask)
            .WithMany()
            .HasForeignKey(t => new {t.StatusTaskId, t.OpenProjectInstanceId})
            .HasPrincipalKey(s => new {s.Id, s.OpenProjectInstanceId})
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(t => t.TasksTimeDetails)
            .WithOne(td => td.Task)
            .HasForeignKey(td => new { td.UserId, td.IdTask })
            .HasPrincipalKey(t => new { t.UserId, t.WorkPackageId })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
