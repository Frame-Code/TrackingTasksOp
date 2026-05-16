using Infrastructure.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.DataAccess.Configurations;

public class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        builder.Property(u => u.OpenProjectUserId)
            .IsRequired();

        builder.Property(u => u.OpenProjectInstanceId)
            .IsRequired();
        
        builder.Property(u => u.OpenProjectInstanceBaseUrl)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(u => u.AuthMethod)
            .IsRequired()
            .HasConversion<string>();

        builder.Property(u => u.CreatedAt)
            .IsRequired()
            .HasColumnType("datetime");

        builder.HasIndex(u => u.OpenProjectUserId);

        builder.HasOne(u => u.OpenProjectInstance)
            .WithMany()
            .HasForeignKey(u => u.OpenProjectInstanceId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
