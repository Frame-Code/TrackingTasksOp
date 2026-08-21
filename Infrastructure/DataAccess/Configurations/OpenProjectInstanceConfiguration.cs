using Domain.Entities.TrackingTasksEntities;
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

        builder.Property(o => o.Alias)
            .HasMaxLength(200);

        builder.Property(o => o.OAuthClientId)
            .HasColumnType("nvarchar(max)");

        builder.Property(o => o.EncryptedOAuthClientSecret)
            .HasColumnType("nvarchar(max)");

        builder.Property(o => o.OAuthConnectedAt)
            .HasColumnType("datetime");

        builder.HasIndex(o => o.BaseUrl)
            .IsUnique();
    }
}
