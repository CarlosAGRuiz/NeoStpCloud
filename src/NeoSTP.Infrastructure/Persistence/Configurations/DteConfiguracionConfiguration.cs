using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NeoSTP.Domain.Core.Dte;

namespace NeoSTP.Infrastructure.Persistence.Configurations;

public class DteConfiguracionConfiguration : IEntityTypeConfiguration<DteConfiguracion>
{
    public void Configure(EntityTypeBuilder<DteConfiguracion> builder)
    {
        builder.ToTable("Dte_Configuracion");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.AmbienteCodigo).HasMaxLength(20).IsRequired();

        builder.Property(c => c.UsuarioMh).HasMaxLength(100);
        builder.Property(c => c.PasswordMhCifrado).HasMaxLength(2000);

        builder.Property(c => c.TipoEstablecimientoCodigo).HasMaxLength(30);
        builder.Property(c => c.CodigoEstablecimientoMh).HasMaxLength(20);
        builder.Property(c => c.CodigoPuntoVentaMh).HasMaxLength(20);

        builder.Property(c => c.CertificadoBlob).HasColumnType("varbinary(max)");
        builder.Property(c => c.CertificadoNombre).HasMaxLength(255);
        builder.Property(c => c.CertificadoHuella).HasMaxLength(100);
        builder.Property(c => c.PasswordCertificadoCifrado).HasMaxLength(2000);

        builder.Property(c => c.UltimaPruebaResultado).HasMaxLength(20);
        builder.Property(c => c.UltimaPruebaDetalle).HasMaxLength(2000);

        builder.Property(c => c.TokenMhCifrado).HasMaxLength(4000);

        builder.Property(c => c.CreatedBy).HasMaxLength(100);
        builder.Property(c => c.UpdatedBy).HasMaxLength(100);

        builder.Ignore(c => c.EsCompleto);

        // 1-a-1: una empresa, una sola configuracion DTE
        builder.HasIndex(c => c.EmpresaId).IsUnique();

        builder.HasOne(c => c.Empresa)
            .WithOne()
            .HasForeignKey<DteConfiguracion>(c => c.EmpresaId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
