using Infrastructure.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.DataAccess.Configurations;

public class UserCredentialConfiguration : IEntityTypeConfiguration<UserCredential>
{
    public void Configure(EntityTypeBuilder<UserCredential> builder)
    {
        builder.ToTable("UserCredentials");

        builder.HasKey(c => c.UserId);

        builder.Property(c => c.UserId)
            .IsRequired()
            .HasMaxLength(450);

        builder.Property(c => c.EncryptedApiKey)
            .HasColumnType("nvarchar(max)");

        builder.Property(c => c.ApiKeyStatus)
            .IsRequired()
            .HasConversion<string>();

        builder.Property(c => c.ApiKeyLastValidatedAt)
            .HasColumnType("datetime");

        builder.Property(c => c.EncryptedOAuthAccessToken)
            .HasColumnType("nvarchar(max)");

        builder.Property(c => c.EncryptedOAuthRefreshToken)
            .HasColumnType("nvarchar(max)");

        builder.Property(c => c.OAuthTokenExpiresAt)
            .HasColumnType("datetime");

        builder.Property(c => c.OAuthScope)
            .HasMaxLength(200);

        builder.Property(c => c.CreatedAt)
            .IsRequired()
            .HasColumnType("datetime");

        builder.HasOne(c => c.User)
            .WithOne()
            .HasForeignKey<UserCredential>(c => c.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
