using Domain.Entities.TrackingTasksEntities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.DataAccess.Configurations;

public class StatusTaskConfiguration : IEntityTypeConfiguration<StatusTask>
{
    public void Configure(EntityTypeBuilder<StatusTask> builder)
    {
        builder.ToTable("StatusTasks");

        builder.HasKey(t => new {t.Id, t.OpenProjectInstanceId});

        builder.Property(t => t.Id)
            .IsRequired()
            .ValueGeneratedNever();

        builder.Property(t => t.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(t => t.IsClosed)
            .IsRequired();

        builder.HasIndex(t => t.OpenProjectInstanceId);

        builder.HasOne(t => t.OpenProjectInstance)
            .WithMany()
            .HasForeignKey(t => t.OpenProjectInstanceId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
