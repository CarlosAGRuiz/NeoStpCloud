using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NeoSTP.Domain.Core.Dte.Certificacion;

namespace NeoSTP.Infrastructure.Persistence.Configurations;

public class CertificacionEscenarioConfiguration : IEntityTypeConfiguration<CertificacionEscenario>
{
    public void Configure(EntityTypeBuilder<CertificacionEscenario> builder)
    {
        builder.ToTable("Dte_CertificacionEscenarios");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Codigo).HasMaxLength(40).IsRequired();
        builder.Property(e => e.Nombre).HasMaxLength(150).IsRequired();
        builder.Property(e => e.Descripcion).HasMaxLength(500);
        builder.Property(e => e.CreatedBy).HasMaxLength(100);
        builder.Property(e => e.UpdatedBy).HasMaxLength(100);

        builder.HasIndex(e => new { e.MatrizId, e.Codigo }).IsUnique();
        builder.HasIndex(e => new { e.MatrizId, e.Activo, e.Orden });
    }
}
