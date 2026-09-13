using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NeoSTP.Domain.Core.Dte;

namespace NeoSTP.Infrastructure.Persistence.Configurations;

public sealed class DteConfiguracionVersionConfiguration : IEntityTypeConfiguration<DteConfiguracionVersion>
{
    public void Configure(EntityTypeBuilder<DteConfiguracionVersion> builder)
    {
        builder.ToTable("Dte_ConfiguracionVersiones");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Motivo).HasMaxLength(50).IsRequired();
        builder.Property(x => x.AmbienteCodigo).HasMaxLength(20).IsRequired();
        builder.Property(x => x.TiposDteAutorizadosCsv).HasMaxLength(100);
        builder.Property(x => x.UsuarioMh).HasMaxLength(100);
        builder.Property(x => x.PasswordMhCifrado).HasMaxLength(2000);
        builder.Property(x => x.TipoEstablecimientoCodigo).HasMaxLength(30);
        builder.Property(x => x.CodigoEstablecimientoMh).HasMaxLength(20);
        builder.Property(x => x.CodigoPuntoVentaMh).HasMaxLength(20);
        builder.Property(x => x.CertificadoBlob).HasColumnType("varbinary(max)");
        builder.Property(x => x.CertificadoNombre).HasMaxLength(255);
        builder.Property(x => x.CertificadoHuella).HasMaxLength(100);
        builder.Property(x => x.PasswordCertificadoCifrado).HasMaxLength(2000);
        builder.Property(x => x.CreatedBy).HasMaxLength(100);
        builder.Property(x => x.UpdatedBy).HasMaxLength(100);
        builder.HasIndex(x => new { x.EmpresaId, x.CreatedAt });
        builder.HasIndex(x => new { x.EmpresaId, x.Id });
        builder.HasOne<DteConfiguracion>()
            .WithMany()
            .HasForeignKey(x => x.ConfiguracionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
