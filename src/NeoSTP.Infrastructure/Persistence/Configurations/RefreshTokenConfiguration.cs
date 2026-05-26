using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NeoSTP.Domain.Core.Seguridad;

namespace NeoSTP.Infrastructure.Persistence.Configurations;

public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("Core_RefreshTokens");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Token).HasMaxLength(500).IsRequired();
        builder.Property(t => t.ReplacedByToken).HasMaxLength(500);
        builder.Property(t => t.CreatedByIp).HasMaxLength(45);
        builder.Property(t => t.RevokedByIp).HasMaxLength(45);
        builder.Property(t => t.RevokedReason).HasMaxLength(500);
        builder.Property(t => t.CreatedBy).HasMaxLength(100);
        builder.Property(t => t.UpdatedBy).HasMaxLength(100);

        builder.Ignore(t => t.IsActive);

        builder.HasIndex(t => t.Token).IsUnique();
        builder.HasIndex(t => new { t.UsuarioId, t.RevokedAt });

        builder.HasOne(t => t.Usuario)
            .WithMany()
            .HasForeignKey(t => t.UsuarioId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
