using Domain.Entities.TrackingTasksEntities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.DataAccess.Configurations;

public class TaskTimeDetailConfiguration : IEntityTypeConfiguration<TaskTimeDetail>
{
    public void Configure(EntityTypeBuilder<TaskTimeDetail> builder)
    {
        builder.ToTable("TaskTimeDetails");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.StartTime)
            .IsRequired()
            .HasColumnType("datetime");
        builder.Property(t => t.EndTime)
            .HasColumnType("datetime");
        builder.Property(t => t.Uploaded)
            .HasColumnType("bit");
    }
}