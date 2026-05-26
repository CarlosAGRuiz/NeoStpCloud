using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NeoSTP.Domain.Core.Empresas;

namespace NeoSTP.Infrastructure.Persistence.Configurations;

public class PuntoVentaConfiguration : IEntityTypeConfiguration<PuntoVenta>
{
    public void Configure(EntityTypeBuilder<PuntoVenta> builder)
    {
        builder.ToTable("Core_PuntosVenta");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Codigo).HasMaxLength(20).IsRequired();
        builder.Property(p => p.Nombre).HasMaxLength(150).IsRequired();
        builder.Property(p => p.CodigoPuntoVentaMh).HasMaxLength(20);
        builder.Property(p => p.EstadoCodigo).HasMaxLength(30).IsRequired();
        builder.Property(p => p.CreatedBy).HasMaxLength(100);
        builder.Property(p => p.UpdatedBy).HasMaxLength(100);

        builder.HasIndex(p => new { p.SucursalId, p.Codigo }).IsUnique();

        builder.HasOne(p => p.Sucursal)
            .WithMany(s => s.PuntosVenta)
            .HasForeignKey(p => p.SucursalId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
