using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NeoSTP.Domain.Core.Compras;

namespace NeoSTP.Infrastructure.Persistence.Configurations;

public class ProveedorConfiguration : IEntityTypeConfiguration<Proveedor>
{
    public void Configure(EntityTypeBuilder<Proveedor> b)
    {
        b.ToTable("Compras_Proveedores");
        b.HasKey(x => x.Id);
        b.Property(x => x.Codigo).HasMaxLength(30).IsRequired();
        b.Property(x => x.Nombre).HasMaxLength(160).IsRequired();
        b.Property(x => x.Nit).HasMaxLength(30);
        b.Property(x => x.Nrc).HasMaxLength(30);
        b.Property(x => x.Contacto).HasMaxLength(120);
        b.Property(x => x.Telefono).HasMaxLength(30);
        b.Property(x => x.Email).HasMaxLength(160);
        b.Property(x => x.Direccion).HasMaxLength(250);
        b.Property(x => x.EstadoCodigo).HasMaxLength(20).IsRequired();
        b.Property(x => x.CreatedBy).HasMaxLength(100);
        b.Property(x => x.UpdatedBy).HasMaxLength(100);

        b.HasOne(x => x.Empresa).WithMany().HasForeignKey(x => x.EmpresaId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.EmpresaId, x.Codigo }).IsUnique();
        b.HasIndex(x => new { x.EmpresaId, x.EstadoCodigo });
    }
}

public class FacturaCompraConfiguration : IEntityTypeConfiguration<FacturaCompra>
{
    public void Configure(EntityTypeBuilder<FacturaCompra> b)
    {
        b.ToTable("Compras_Facturas");
        b.HasKey(x => x.Id);
        b.Property(x => x.NumeroDocumento).HasMaxLength(50).IsRequired();
        b.Property(x => x.TipoDocumento).HasMaxLength(20).IsRequired();
        b.Property(x => x.CondicionPago).HasMaxLength(20).IsRequired();
        b.Property(x => x.Subtotal).HasPrecision(18, 2);
        b.Property(x => x.Iva).HasPrecision(18, 2);
        b.Property(x => x.Total).HasPrecision(18, 2);
        b.Property(x => x.Descripcion).HasMaxLength(250);
        b.Property(x => x.EstadoCodigo).HasMaxLength(20).IsRequired();
        b.Property(x => x.CreatedBy).HasMaxLength(100);
        b.Property(x => x.UpdatedBy).HasMaxLength(100);

        b.HasOne(x => x.Empresa).WithMany().HasForeignKey(x => x.EmpresaId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Proveedor).WithMany(p => p.Facturas).HasForeignKey(x => x.ProveedorId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.EmpresaId, x.EstadoCodigo });
        b.HasIndex(x => new { x.EmpresaId, x.ProveedorId, x.NumeroDocumento });
    }
}

public class PagoProveedorConfiguration : IEntityTypeConfiguration<PagoProveedor>
{
    public void Configure(EntityTypeBuilder<PagoProveedor> b)
    {
        b.ToTable("Compras_Pagos");
        b.HasKey(x => x.Id);
        b.Property(x => x.Monto).HasPrecision(18, 2);
        b.Property(x => x.FormaPagoCodigo).HasMaxLength(20).IsRequired();
        b.Property(x => x.Referencia).HasMaxLength(80);
        b.Property(x => x.Nota).HasMaxLength(250);
        b.Property(x => x.EstadoCodigo).HasMaxLength(20).IsRequired();
        b.Property(x => x.CreatedBy).HasMaxLength(100);
        b.Property(x => x.UpdatedBy).HasMaxLength(100);

        b.HasOne(x => x.FacturaCompra).WithMany(f => f.Pagos).HasForeignKey(x => x.FacturaCompraId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => new { x.EmpresaId, x.FacturaCompraId });
        b.HasIndex(x => new { x.EmpresaId, x.EstadoCodigo });
    }
}

public class OrdenCompraConfiguration : IEntityTypeConfiguration<OrdenCompra>
{
    public void Configure(EntityTypeBuilder<OrdenCompra> b)
    {
        b.ToTable("Compras_Ordenes");
        b.HasKey(x => x.Id);
        b.Property(x => x.Numero).HasMaxLength(40).IsRequired();
        b.Property(x => x.EstadoCodigo).HasMaxLength(20).IsRequired();
        b.Property(x => x.MonedaCodigo).HasMaxLength(3).IsRequired();
        b.Property(x => x.Observaciones).HasMaxLength(1000);
        b.Property(x => x.Subtotal).HasPrecision(18, 2);
        b.Property(x => x.Iva).HasPrecision(18, 2);
        b.Property(x => x.Total).HasPrecision(18, 2);
        b.Property(x => x.CreatedBy).HasMaxLength(100);
        b.Property(x => x.UpdatedBy).HasMaxLength(100);

        b.HasOne(x => x.Empresa).WithMany().HasForeignKey(x => x.EmpresaId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Proveedor).WithMany().HasForeignKey(x => x.ProveedorId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.FacturaCompra).WithMany().HasForeignKey(x => x.FacturaCompraId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.Lineas).WithOne(x => x.OrdenCompra).HasForeignKey(x => x.OrdenCompraId).OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(x => new { x.EmpresaId, x.Numero }).IsUnique();
        b.HasIndex(x => new { x.EmpresaId, x.EstadoCodigo, x.Fecha });
        b.HasIndex(x => new { x.EmpresaId, x.ProveedorId });
        b.HasIndex(x => x.FacturaCompraId).IsUnique().HasFilter("[FacturaCompraId] IS NOT NULL");
    }
}

public class OrdenCompraLineaConfiguration : IEntityTypeConfiguration<OrdenCompraLinea>
{
    public void Configure(EntityTypeBuilder<OrdenCompraLinea> b)
    {
        b.ToTable("Compras_OrdenLineas");
        b.HasKey(x => x.Id);
        b.Property(x => x.Descripcion).HasMaxLength(250).IsRequired();
        b.Property(x => x.UnidadMedidaCodigo).HasMaxLength(10).IsRequired();
        b.Property(x => x.Cantidad).HasPrecision(18, 4);
        b.Property(x => x.PrecioUnitario).HasPrecision(18, 4);
        b.Property(x => x.Subtotal).HasPrecision(18, 2);
        b.Property(x => x.Iva).HasPrecision(18, 2);
        b.Property(x => x.Total).HasPrecision(18, 2);
        b.Property(x => x.CreatedBy).HasMaxLength(100);
        b.Property(x => x.UpdatedBy).HasMaxLength(100);

        b.HasOne(x => x.Empresa).WithMany().HasForeignKey(x => x.EmpresaId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Producto).WithMany().HasForeignKey(x => x.ProductoId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.EmpresaId, x.OrdenCompraId });
        b.HasIndex(x => new { x.OrdenCompraId, x.NumeroLinea }).IsUnique();
        b.HasIndex(x => x.ProductoId);
    }
}

public class OrdenCompraRecepcionConfiguration : IEntityTypeConfiguration<OrdenCompraRecepcion>
{
    public void Configure(EntityTypeBuilder<OrdenCompraRecepcion> b)
    {
        b.ToTable("Compras_OrdenRecepciones");
        b.HasKey(x => x.Id);
        b.Property(x => x.Numero).HasMaxLength(40).IsRequired();
        b.Property(x => x.IdempotencyKey).HasMaxLength(64).IsRequired();
        b.Property(x => x.Referencia).HasMaxLength(80);
        b.Property(x => x.Observaciones).HasMaxLength(500);
        b.Property(x => x.CreatedBy).HasMaxLength(100);
        b.Property(x => x.UpdatedBy).HasMaxLength(100);

        b.HasOne(x => x.Empresa).WithMany().HasForeignKey(x => x.EmpresaId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.OrdenCompra).WithMany(x => x.Recepciones).HasForeignKey(x => x.OrdenCompraId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(x => x.Lineas).WithOne(x => x.Recepcion).HasForeignKey(x => x.OrdenCompraRecepcionId).OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(x => new { x.EmpresaId, x.Numero }).IsUnique();
        b.HasIndex(x => new { x.EmpresaId, x.IdempotencyKey }).IsUnique();
        b.HasIndex(x => new { x.EmpresaId, x.OrdenCompraId, x.Fecha });
    }
}

public class OrdenCompraRecepcionLineaConfiguration : IEntityTypeConfiguration<OrdenCompraRecepcionLinea>
{
    public void Configure(EntityTypeBuilder<OrdenCompraRecepcionLinea> b)
    {
        b.ToTable("Compras_OrdenRecepcionLineas");
        b.HasKey(x => x.Id);
        b.Property(x => x.Cantidad).HasPrecision(18, 4);
        b.Property(x => x.CreatedBy).HasMaxLength(100);
        b.Property(x => x.UpdatedBy).HasMaxLength(100);

        b.HasOne(x => x.Empresa).WithMany().HasForeignKey(x => x.EmpresaId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.OrdenCompraLinea).WithMany(x => x.Recepciones).HasForeignKey(x => x.OrdenCompraLineaId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.MovimientoInventario).WithMany().HasForeignKey(x => x.MovimientoInventarioId).OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(x => new { x.OrdenCompraRecepcionId, x.OrdenCompraLineaId }).IsUnique();
        b.HasIndex(x => x.OrdenCompraLineaId);
        b.HasIndex(x => x.MovimientoInventarioId).IsUnique().HasFilter("[MovimientoInventarioId] IS NOT NULL");
    }
}
