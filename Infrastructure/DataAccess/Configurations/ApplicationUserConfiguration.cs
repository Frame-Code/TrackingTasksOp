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
            .IsRequired();

        builder.Property(u => u.PauseDefaultBehavior)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(u => u.SkipCancelConfirmation)
            .IsRequired();

        builder.Property(u => u.IsAppAdmin)
            .IsRequired();

        builder.Property(u => u.AddRandomSlackTime)
            .IsRequired();

        builder.Property(u => u.EncryptedGroqApiKey);

        builder.Property(u => u.DefaultStatusFilterIds);

        builder.Property(u => u.PasswordResetCodeHash)
            .HasMaxLength(64);

        builder.Property(u => u.PasswordResetCodeExpiresAt);

        builder.HasIndex(u => u.OpenProjectUserId);

        builder.HasOne(u => u.OpenProjectInstance)
            .WithMany()
            .HasForeignKey(u => u.OpenProjectInstanceId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
