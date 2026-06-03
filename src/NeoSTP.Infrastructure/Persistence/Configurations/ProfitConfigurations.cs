using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NeoSTP.Domain.Core.Profit;

namespace NeoSTP.Infrastructure.Persistence.Configurations;

public class ProfitGastoConfiguration : IEntityTypeConfiguration<ProfitGasto>
{
    public void Configure(EntityTypeBuilder<ProfitGasto> b)
    {
        b.ToTable("Profit_Gastos");
        b.HasKey(x => x.Id);
        b.Property(x => x.Categoria).HasMaxLength(40).IsRequired();
        b.Property(x => x.Descripcion).HasMaxLength(300).IsRequired();
        b.Property(x => x.Proveedor).HasMaxLength(160);
        b.Property(x => x.Monto).HasPrecision(18, 2);
        b.Property(x => x.IvaMonto).HasPrecision(18, 2);
        b.Property(x => x.EstadoCodigo).HasMaxLength(20).IsRequired();
        b.Property(x => x.CreatedBy).HasMaxLength(100);
        b.Property(x => x.UpdatedBy).HasMaxLength(100);
        b.Ignore(x => x.Total);

        b.HasOne(x => x.Empresa).WithMany().HasForeignKey(x => x.EmpresaId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.EmpresaId, x.Fecha });
        b.HasIndex(x => new { x.EmpresaId, x.EstadoCodigo });
    }
}

public class ProfitCompraConfiguration : IEntityTypeConfiguration<ProfitCompra>
{
    public void Configure(EntityTypeBuilder<ProfitCompra> b)
    {
        b.ToTable("Profit_Compras");
        b.HasKey(x => x.Id);
        b.Property(x => x.Proveedor).HasMaxLength(160).IsRequired();
        b.Property(x => x.NumeroDocumento).HasMaxLength(60);
        b.Property(x => x.Descripcion).HasMaxLength(300);
        b.Property(x => x.Subtotal).HasPrecision(18, 2);
        b.Property(x => x.IvaMonto).HasPrecision(18, 2);
        b.Property(x => x.EstadoCodigo).HasMaxLength(20).IsRequired();
        b.Property(x => x.CreatedBy).HasMaxLength(100);
        b.Property(x => x.UpdatedBy).HasMaxLength(100);
        b.Ignore(x => x.Total);

        b.HasOne(x => x.Empresa).WithMany().HasForeignKey(x => x.EmpresaId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.EmpresaId, x.Fecha });
        b.HasIndex(x => new { x.EmpresaId, x.EstadoCodigo });
    }
}
