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
