using Domain.Entities.TrackingTasksEntities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.DataAccess.Configurations;

public class MigrationDataConfiguration : IEntityTypeConfiguration<MigrationData>
{
    public void Configure(EntityTypeBuilder<MigrationData> builder)
    {
        builder.ToTable("MigrationsData");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Name)
            .IsRequired()
            .HasMaxLength(200);
        builder.Property(p => p.CreatedAt)
            .IsRequired();
        builder.Property(p => p.Description)
            .IsRequired()
            .HasMaxLength(1000);
    }
}