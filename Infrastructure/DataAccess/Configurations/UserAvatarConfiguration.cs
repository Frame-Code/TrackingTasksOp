using Infrastructure.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.DataAccess.Configurations;

public class UserAvatarConfiguration : IEntityTypeConfiguration<UserAvatar>
{
    public void Configure(EntityTypeBuilder<UserAvatar> builder)
    {
        builder.ToTable("UserAvatars");

        builder.HasKey(a => a.UserId);

        builder.Property(a => a.UserId)
            .IsRequired()
            .HasMaxLength(450);

        // Sin HasColumnType: EF mapea byte[] a bytea solo. Dejarle el tipo al proveedor es lo
        // que permitió migrar de SQL Server a Postgres sin tocar esta clase.
        builder.Property(a => a.Jpeg)
            .IsRequired();

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(a => a.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
