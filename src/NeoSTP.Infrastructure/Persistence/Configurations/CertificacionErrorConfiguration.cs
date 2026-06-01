using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NeoSTP.Domain.Core.Dte.Certificacion;

namespace NeoSTP.Infrastructure.Persistence.Configurations;

public class CertificacionErrorConfiguration : IEntityTypeConfiguration<CertificacionError>
{
    public void Configure(EntityTypeBuilder<CertificacionError> builder)
    {
        builder.ToTable("Dte_CertificacionErrores");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.CodigoMh).HasMaxLength(30).IsRequired();
        builder.Property(e => e.Descripcion).HasMaxLength(500).IsRequired();
        builder.Property(e => e.RespuestaMhJson).HasColumnType("nvarchar(max)");
        builder.Property(e => e.CreatedBy).HasMaxLength(100);
        builder.Property(e => e.UpdatedBy).HasMaxLength(100);

        builder.HasIndex(e => new { e.PruebaId, e.OcurrioAt });
        builder.HasIndex(e => e.CodigoMh);
    }
}
