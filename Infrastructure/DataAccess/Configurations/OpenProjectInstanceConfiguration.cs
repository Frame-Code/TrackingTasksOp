using Domain.Entities.TrackingTasksEntities;
using Infrastructure.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.DataAccess.Configurations;

public class OpenProjectInstanceConfiguration : IEntityTypeConfiguration<OpenProjectInstance>
{
    public void Configure(EntityTypeBuilder<OpenProjectInstance> builder)
    {
        builder.ToTable("OpenProjectInstances");

        builder.HasKey(o => o.Id);

        builder.Property(o => o.Id)
            .IsRequired()
            .ValueGeneratedOnAdd();

        builder.Property(o => o.BaseUrl)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(o => o.CreatedAt)
            .IsRequired()
            .HasColumnType("datetime");

        builder.HasIndex(o => o.BaseUrl)
            .IsUnique();
    }
}
