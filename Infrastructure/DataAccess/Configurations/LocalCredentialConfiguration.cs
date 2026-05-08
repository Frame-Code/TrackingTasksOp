using Infrastructure.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.DataAccess.Configurations;

public class LocalCredentialConfiguration : IEntityTypeConfiguration<LocalCredential>
{
    public void Configure(EntityTypeBuilder<LocalCredential> builder)
    {
        builder.ToTable("LocalCredentials");

        builder.Property(c => c.EncryptedApiKey)
            .HasColumnType("nvarchar(max)");

        builder.Property(c => c.ApiKeyStatus)
            .IsRequired()
            .HasConversion<string>();

        builder.Property(c => c.ApiKeyLastValidatedAt)
            .HasColumnType("datetime");
        
        builder.HasOne(c => c.ApplicationUser)
            .WithOne()
            .HasForeignKey<UserCredential>(c => c.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}