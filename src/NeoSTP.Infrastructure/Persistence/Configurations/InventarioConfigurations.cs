using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NeoSTP.Domain.Core.Inventario;

namespace NeoSTP.Infrastructure.Persistence.Configurations;

public class ExistenciaProductoConfiguration : IEntityTypeConfiguration<ExistenciaProducto>
{
    public void Configure(EntityTypeBuilder<ExistenciaProducto> b)
    {
        b.ToTable("Inv_Existencias");
        b.HasKey(x => x.Id);
        b.Property(x => x.Cantidad).HasPrecision(18, 4);
        b.Property(x => x.CostoPromedio).HasPrecision(18, 4);
        b.Property(x => x.StockMinimo).HasPrecision(18, 4);
        b.Property(x => x.CreatedBy).HasMaxLength(100);
        b.Property(x => x.UpdatedBy).HasMaxLength(100);

        b.HasOne(x => x.Empresa).WithMany().HasForeignKey(x => x.EmpresaId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Producto).WithMany().HasForeignKey(x => x.ProductoId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => new { x.EmpresaId, x.ProductoId }).IsUnique();
    }
}

public class MovimientoInventarioConfiguration : IEntityTypeConfiguration<MovimientoInventario>
{
    public void Configure(EntityTypeBuilder<MovimientoInventario> b)
    {
        b.ToTable("Inv_Movimientos");
        b.HasKey(x => x.Id);
        b.Property(x => x.Tipo).HasMaxLength(20).IsRequired();
        b.Property(x => x.Cantidad).HasPrecision(18, 4);
        b.Property(x => x.CostoUnitario).HasPrecision(18, 4);
        b.Property(x => x.Origen).HasMaxLength(20).IsRequired();
        b.Property(x => x.Referencia).HasMaxLength(80);
        b.Property(x => x.Nota).HasMaxLength(250);
        b.Property(x => x.SaldoCantidad).HasPrecision(18, 4);
        b.Property(x => x.SaldoCostoPromedio).HasPrecision(18, 4);
        b.Property(x => x.CreatedBy).HasMaxLength(100);
        b.Property(x => x.UpdatedBy).HasMaxLength(100);

        b.HasIndex(x => new { x.EmpresaId, x.ProductoId, x.Fecha });
        b.HasIndex(x => new { x.EmpresaId, x.Origen, x.OrigenId });
    }
}
