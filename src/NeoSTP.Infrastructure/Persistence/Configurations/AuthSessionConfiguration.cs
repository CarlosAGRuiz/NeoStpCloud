using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NeoSTP.Domain.Core.Seguridad;

namespace NeoSTP.Infrastructure.Persistence.Configurations;

public class AuthSessionConfiguration : IEntityTypeConfiguration<AuthSession>
{
    public void Configure(EntityTypeBuilder<AuthSession> builder)
    {
        builder.ToTable("Core_AuthSessions");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Purpose).HasMaxLength(20).IsRequired();
        builder.Property(s => s.CredentialFingerprint).HasMaxLength(64).IsRequired();
        builder.Property(s => s.AuthorizationFingerprint).HasMaxLength(64).IsRequired();
        builder.Property(s => s.RevokedAt).IsConcurrencyToken();
        builder.HasIndex(s => new { s.UsuarioId, s.ExpiresAt });
        builder.HasOne(s => s.Usuario).WithMany().HasForeignKey(s => s.UsuarioId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
