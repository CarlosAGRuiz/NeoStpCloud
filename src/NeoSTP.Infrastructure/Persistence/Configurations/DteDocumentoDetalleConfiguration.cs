using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NeoSTP.Domain.Core.Dte;

namespace NeoSTP.Infrastructure.Persistence.Configurations;

public class DteDocumentoDetalleConfiguration : IEntityTypeConfiguration<DteDocumentoDetalle>
{
    public void Configure(EntityTypeBuilder<DteDocumentoDetalle> builder)
    {
        builder.ToTable("Dte_DocumentoDetalles");
        builder.HasKey(d => d.Id);

        builder.Property(d => d.Codigo).HasMaxLength(50).IsRequired();
        builder.Property(d => d.Descripcion).HasMaxLength(500).IsRequired();
        builder.Property(d => d.UnidadMedidaCodigo).HasMaxLength(20).IsRequired();
        builder.Property(d => d.Observaciones).HasMaxLength(500);

        builder.Property(d => d.Cantidad).HasPrecision(18, 4);
        builder.Property(d => d.PrecioUnitario).HasPrecision(18, 4);
        builder.Property(d => d.MontoDescuento).HasPrecision(18, 4);
        builder.Property(d => d.VentaNoSujeta).HasPrecision(18, 4);
        builder.Property(d => d.VentaExenta).HasPrecision(18, 4);
        builder.Property(d => d.VentaGravada).HasPrecision(18, 4);
        builder.Property(d => d.IvaItem).HasPrecision(18, 4);

        builder.Property(d => d.CreatedBy).HasMaxLength(100);
        builder.Property(d => d.UpdatedBy).HasMaxLength(100);

        builder.HasIndex(d => new { d.DocumentoId, d.NumeroLinea }).IsUnique();

        builder.HasOne(d => d.Producto)
            .WithMany()
            .HasForeignKey(d => d.ProductoId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
