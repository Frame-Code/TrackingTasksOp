using Infrastructure.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.DataAccess.Configurations;

public class OAuthCredentialConfiguration : IEntityTypeConfiguration<OAuthCredential>
{
    public void Configure(EntityTypeBuilder<OAuthCredential> builder)
    {
        builder.ToTable("OAuthCredentials");
        
        builder.Property(c => c.EncryptedOAuthAccessToken);

        builder.Property(c => c.EncryptedOAuthRefreshToken);

        builder.Property(c => c.OAuthTokenExpiresAt);

        builder.Property(c => c.OAuthScope)
            .HasMaxLength(200);

        builder.Property(c => c.CreatedAt)
            .IsRequired();

        builder.HasOne(c => c.ApplicationUser)
            .WithOne()
            .HasForeignKey<OAuthCredential>(c => c.UserId)   // ← OAuthCredential, no UserCredential
            .OnDelete(DeleteBehavior.Cascade);
    }
}