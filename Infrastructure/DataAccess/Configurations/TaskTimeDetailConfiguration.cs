using Domain.Entities.TrackingTasksEntities;
using Infrastructure.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.DataAccess.Configurations;

public class TaskTimeDetailConfiguration : IEntityTypeConfiguration<TaskTimeDetail>
{
    public void Configure(EntityTypeBuilder<TaskTimeDetail> builder)
    {
        builder.ToTable("TaskTimeDetails");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.UserId)
            .IsRequired()
            .HasMaxLength(450);

        builder.Property(t => t.StartTime)
            .IsRequired();

        builder.Property(t => t.EndTime);

        builder.Property(t => t.Uploaded);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
