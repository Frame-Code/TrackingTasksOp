using Infrastructure.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.DataAccess.Configurations;

public class AuthAuditLogConfiguration : IEntityTypeConfiguration<AuthAuditLog>
{
    public void Configure(EntityTypeBuilder<AuthAuditLog> builder)
    {
        builder.ToTable("AuthAuditLogs");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id)
            .IsRequired()
            .ValueGeneratedOnAdd();

        builder.Property(a => a.UserId)
            .HasMaxLength(450);

        builder.Property(a => a.EventType)
            .IsRequired()
            .HasConversion<string>();

        builder.Property(a => a.IpAddress)
            .IsRequired()
            .HasMaxLength(45);

        builder.Property(a => a.UserAgent)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(a => a.Detail)
            .IsRequired()
            .HasColumnType("nvarchar(max)");

        builder.Property(a => a.CreatedAt)
            .IsRequired()
            .HasColumnType("datetime");

        builder.HasIndex(a => new { a.UserId, a.CreatedAt })
            .IsDescending(false, true);

        builder.HasIndex(a => new { a.IpAddress, a.EventType, a.CreatedAt })
            .IsDescending(false, false, true);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(a => a.UserId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
