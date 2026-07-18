using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NeoSTP.Domain.Core.Productos;

namespace NeoSTP.Infrastructure.Persistence.Configurations;

public class ProductoConfiguration : IEntityTypeConfiguration<Producto>
{
    public void Configure(EntityTypeBuilder<Producto> builder)
    {
        builder.ToTable("Dte_Productos");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.CodigoInterno).HasMaxLength(50).IsRequired();
        builder.Property(p => p.CodigoBarra).HasMaxLength(50);
        builder.Property(p => p.Nombre).HasMaxLength(250).IsRequired();
        builder.Property(p => p.Descripcion).HasMaxLength(1000);
        builder.Property(p => p.TipoItem).HasMaxLength(20).IsRequired();
        builder.Property(p => p.CategoriaCodigo).HasMaxLength(30);
        builder.Property(p => p.UnidadMedidaCodigo).HasMaxLength(20).IsRequired();
        builder.Property(p => p.PrecioUnitario).HasPrecision(18, 4);
        builder.Property(p => p.CostoUnitario).HasPrecision(18, 4);
        builder.Property(p => p.TributoCodigo).HasMaxLength(20);
        builder.Property(p => p.EstadoCodigo).HasMaxLength(30).IsRequired();
        builder.Property(p => p.CreatedBy).HasMaxLength(100);
        builder.Property(p => p.UpdatedBy).HasMaxLength(100);

        builder.Ignore(p => p.EsServicio);

        builder.HasIndex(p => new { p.EmpresaId, p.CodigoInterno }).IsUnique();
        builder.HasIndex(p => new { p.EmpresaId, p.CodigoBarra });
        builder.HasIndex(p => new { p.EmpresaId, p.Nombre });
        builder.HasIndex(p => new { p.EmpresaId, p.CategoriaCodigo });
        builder.HasIndex(p => p.EstadoCodigo);

        builder.HasOne(p => p.Empresa)
            .WithMany()
            .HasForeignKey(p => p.EmpresaId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class ProductoPrecioEscalaConfiguration : IEntityTypeConfiguration<ProductoPrecioEscala>
{
    public void Configure(EntityTypeBuilder<ProductoPrecioEscala> b)
    {
        b.ToTable("Prod_PreciosEscala");
        b.HasKey(x => x.Id);
        b.Property(x => x.CantidadMinima).HasPrecision(18, 4);
        b.Property(x => x.PrecioUnitario).HasPrecision(18, 4);
        b.Property(x => x.CreatedBy).HasMaxLength(100);
        b.Property(x => x.UpdatedBy).HasMaxLength(100);

        b.HasOne(x => x.Empresa).WithMany().HasForeignKey(x => x.EmpresaId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Producto).WithMany().HasForeignKey(x => x.ProductoId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => new { x.EmpresaId, x.ProductoId, x.CantidadMinima }).IsUnique();
    }
}

public class ProductoUnidadAlternativaConfiguration : IEntityTypeConfiguration<ProductoUnidadAlternativa>
{
    public void Configure(EntityTypeBuilder<ProductoUnidadAlternativa> b)
    {
        b.ToTable("Prod_UnidadesAlternativas");
        b.HasKey(x => x.Id);
        b.Property(x => x.UnidadMedidaCodigo).HasMaxLength(20).IsRequired();
        b.Property(x => x.Nombre).HasMaxLength(60).IsRequired();
        b.Property(x => x.Factor).HasPrecision(18, 4);
        b.Property(x => x.PrecioUnitario).HasPrecision(18, 4);
        b.Property(x => x.CreatedBy).HasMaxLength(100);
        b.Property(x => x.UpdatedBy).HasMaxLength(100);

        b.HasOne(x => x.Empresa).WithMany().HasForeignKey(x => x.EmpresaId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Producto).WithMany().HasForeignKey(x => x.ProductoId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => new { x.EmpresaId, x.ProductoId, x.UnidadMedidaCodigo }).IsUnique();
    }
}
