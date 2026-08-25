using Domain.Entities.TrackingTasksEntities;
using Infrastructure.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.DataAccess.Configurations;

public class MigrationDataConfiguration : IEntityTypeConfiguration<MigrationData>
{
    public void Configure(EntityTypeBuilder<MigrationData> builder)
    {
        builder.ToTable("MigrationsData");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.UserId)
            .IsRequired()
            .HasMaxLength(450);

        builder.Property(p => p.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(p => p.Description)
            .IsRequired()
            .HasMaxLength(1000);

        builder.Property(p => p.CreatedAt)
            .IsRequired();

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(p => p.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(p => p.OpenProjectInstance)
            .WithMany()
            .HasForeignKey(p => p.OpenProjectInstanceId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
