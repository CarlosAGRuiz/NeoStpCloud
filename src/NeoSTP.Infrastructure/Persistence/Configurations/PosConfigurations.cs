using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NeoSTP.Domain.Core.Pos;

namespace NeoSTP.Infrastructure.Persistence.Configurations;

public class VentaPosConfiguration : IEntityTypeConfiguration<VentaPos>
{
    public void Configure(EntityTypeBuilder<VentaPos> b)
    {
        b.ToTable("Pos_Ventas");
        b.HasKey(x => x.Id);
        b.Property(x => x.Numero).HasMaxLength(30).IsRequired();
        b.Property(x => x.ClienteNombre).HasMaxLength(160).IsRequired();
        b.Property(x => x.FormaPagoCodigo).HasMaxLength(20).IsRequired();
        b.Property(x => x.EfectivoRecibido).HasPrecision(18, 2);
        b.Property(x => x.Cambio).HasPrecision(18, 2);
        b.Property(x => x.Subtotal).HasPrecision(18, 2);
        b.Property(x => x.IvaTotal).HasPrecision(18, 2);
        b.Property(x => x.TotalDescuento).HasPrecision(18, 2);
        b.Property(x => x.Total).HasPrecision(18, 2);
        b.Property(x => x.Nota).HasMaxLength(250);
        b.Property(x => x.EstadoCodigo).HasMaxLength(20).IsRequired();
        b.Property(x => x.EstadoFacturacion).HasMaxLength(20).IsRequired();
        b.Property(x => x.CreatedBy).HasMaxLength(100);
        b.Property(x => x.UpdatedBy).HasMaxLength(100);

        b.HasOne(x => x.Empresa).WithMany().HasForeignKey(x => x.EmpresaId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.EmpresaId, x.Numero }).IsUnique();
        b.HasIndex(x => new { x.EmpresaId, x.Fecha });
        b.HasIndex(x => new { x.EmpresaId, x.EstadoCodigo });
        b.HasIndex(x => x.SesionCajaId);
    }
}

public class SesionCajaConfiguration : IEntityTypeConfiguration<SesionCaja>
{
    public void Configure(EntityTypeBuilder<SesionCaja> b)
    {
        b.ToTable("Pos_SesionesCaja");
        b.HasKey(x => x.Id);
        b.Property(x => x.Numero).HasMaxLength(30).IsRequired();
        b.Property(x => x.EstadoCodigo).HasMaxLength(20).IsRequired();
        b.Property(x => x.MontoInicial).HasPrecision(18, 2);
        b.Property(x => x.MontoEsperado).HasPrecision(18, 2);
        b.Property(x => x.MontoContado).HasPrecision(18, 2);
        b.Property(x => x.Diferencia).HasPrecision(18, 2);
        b.Property(x => x.AbiertaPor).HasMaxLength(100);
        b.Property(x => x.CerradaPor).HasMaxLength(100);
        b.Property(x => x.Nota).HasMaxLength(250);
        b.Property(x => x.CreatedBy).HasMaxLength(100);
        b.Property(x => x.UpdatedBy).HasMaxLength(100);

        b.HasOne(x => x.Empresa).WithMany().HasForeignKey(x => x.EmpresaId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.EmpresaId, x.Numero }).IsUnique();
        b.HasIndex(x => new { x.EmpresaId, x.EstadoCodigo });
    }
}

public class VentaPosLineaConfiguration : IEntityTypeConfiguration<VentaPosLinea>
{
    public void Configure(EntityTypeBuilder<VentaPosLinea> b)
    {
        b.ToTable("Pos_VentaLineas");
        b.HasKey(x => x.Id);
        b.Property(x => x.Codigo).HasMaxLength(50).IsRequired();
        b.Property(x => x.Descripcion).HasMaxLength(250).IsRequired();
        b.Property(x => x.Cantidad).HasPrecision(18, 4);
        b.Property(x => x.PrecioUnitario).HasPrecision(18, 2);
        b.Property(x => x.Descuento).HasPrecision(18, 2);
        b.Property(x => x.IvaLinea).HasPrecision(18, 2);
        b.Property(x => x.Total).HasPrecision(18, 2);
        b.Property(x => x.CreatedBy).HasMaxLength(100);
        b.Property(x => x.UpdatedBy).HasMaxLength(100);

        b.HasOne(x => x.VentaPos).WithMany(v => v.Lineas).HasForeignKey(x => x.VentaPosId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => x.VentaPosId);
    }
}

public class ImpresoraPosConfiguration : IEntityTypeConfiguration<ImpresoraPos>
{
    public void Configure(EntityTypeBuilder<ImpresoraPos> b)
    {
        b.ToTable("Pos_Impresoras");
        b.HasKey(x => x.Id);
        b.Property(x => x.Nombre).HasMaxLength(80).IsRequired();
        b.Property(x => x.Conexion).HasMaxLength(20).IsRequired();
        b.Property(x => x.Ip).HasMaxLength(60);
        b.Property(x => x.EstadoCodigo).HasMaxLength(20).IsRequired();
        b.Property(x => x.Notas).HasMaxLength(250);
        b.Property(x => x.CreatedBy).HasMaxLength(100);
        b.Property(x => x.UpdatedBy).HasMaxLength(100);

        b.HasOne(x => x.Empresa).WithMany().HasForeignKey(x => x.EmpresaId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.EmpresaId, x.EstadoCodigo });
    }
}
