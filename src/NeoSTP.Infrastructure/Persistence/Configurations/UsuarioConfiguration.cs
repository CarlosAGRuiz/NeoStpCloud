using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NeoSTP.Domain.Core.Seguridad;

namespace NeoSTP.Infrastructure.Persistence.Configurations;

public class UsuarioConfiguration : IEntityTypeConfiguration<Usuario>
{
    public void Configure(EntityTypeBuilder<Usuario> builder)
    {
        builder.ToTable("Core_Usuarios");
        builder.HasKey(u => u.Id);

        builder.Property(u => u.Username).HasMaxLength(100).IsRequired();
        builder.Property(u => u.Email).HasMaxLength(200).IsRequired();
        builder.Property(u => u.PasswordHash).HasMaxLength(500).IsRequired();
        builder.Property(u => u.NombreCompleto).HasMaxLength(200).IsRequired();
        builder.Property(u => u.Telefono).HasMaxLength(30);
        builder.Property(u => u.TipoUsuarioCodigo).HasMaxLength(30).IsRequired();
        builder.Property(u => u.EstadoCodigo).HasMaxLength(30).IsRequired();
        builder.Property(u => u.CreatedBy).HasMaxLength(100);
        builder.Property(u => u.UpdatedBy).HasMaxLength(100);

        // MFA (TOTP) — Sprint 20
        builder.Property(u => u.MfaSecretoCifrado).HasMaxLength(500);
        builder.Property(u => u.MfaRecoveryCodesJson).HasColumnType("nvarchar(max)");

        // SSO federado (E3)
        builder.Property(u => u.SsoProveedor).HasMaxLength(20);
        builder.Property(u => u.SsoSubject).HasMaxLength(200);

        builder.HasIndex(u => new { u.EmpresaId, u.Username }).IsUnique();
        builder.HasIndex(u => new { u.EmpresaId, u.Email }).IsUnique();
        builder.HasIndex(u => u.EstadoCodigo);

        // Un sujeto federado se vincula a una sola cuenta local (filtrado: solo cuentas SSO).
        builder.HasIndex(u => new { u.SsoProveedor, u.SsoSubject })
            .IsUnique()
            .HasFilter("[SsoProveedor] IS NOT NULL AND [SsoSubject] IS NOT NULL");

        builder.HasOne(u => u.Empresa)
            .WithMany()
            .HasForeignKey(u => u.EmpresaId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
