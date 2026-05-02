using Domain.Entities.TrackingTasksEntities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.DataAccess.Configurations;

public class StatusTaskConfiguration : IEntityTypeConfiguration<StatusTask>
{
    public void Configure(EntityTypeBuilder<StatusTask> builder)
    {
        builder.ToTable("StatusTasks");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id)
            .IsRequired()
            .ValueGeneratedNever();
        builder.Property(t => t.Name)
            .IsRequired()
            .HasMaxLength(200);
        builder.Property(t => t.IsClosed)
            .IsRequired();
    }
}