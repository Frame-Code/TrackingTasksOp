using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.DataAccess.Configurations;

public class TaskConfiguration: IEntityTypeConfiguration<Domain.Entities.TrackingTasksEntities.Task>
{
    public void Configure(EntityTypeBuilder<Domain.Entities.TrackingTasksEntities.Task> builder)
    {
        builder.ToTable("Tasks");
        builder.HasKey(t => t.WorkPackageId);
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
              
        builder.HasOne(t => t.Project)
            .WithMany()
            .HasForeignKey(t => t.ProjectId)
            .OnDelete(DeleteBehavior.Restrict);
              
        builder.HasOne(t => t.StatusTask)
            .WithMany()
            .HasForeignKey(t => t.StatusTaskId)
            .OnDelete(DeleteBehavior.Restrict);
              
        builder.HasMany(t => t.TasksTimeDetails)
            .WithOne(t => t.Task)
            .HasForeignKey(t => t.IdTask)
            .HasPrincipalKey(t => t.WorkPackageId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}