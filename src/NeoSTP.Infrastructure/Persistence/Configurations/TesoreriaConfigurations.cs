using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NeoSTP.Domain.Core.Tesoreria;

namespace NeoSTP.Infrastructure.Persistence.Configurations;

public class CuentaTesoreriaConfiguration : IEntityTypeConfiguration<CuentaTesoreria>
{
    public void Configure(EntityTypeBuilder<CuentaTesoreria> b)
    {
        b.ToTable("Tes_Cuentas");
        b.HasKey(x => x.Id);
        b.Property(x => x.Codigo).HasMaxLength(20).IsRequired();
        b.Property(x => x.Nombre).HasMaxLength(120).IsRequired();
        b.Property(x => x.TipoCuenta).HasMaxLength(20).IsRequired();
        b.Property(x => x.Banco).HasMaxLength(80);
        b.Property(x => x.NumeroCuenta).HasMaxLength(40);
        b.Property(x => x.MonedaCodigo).HasMaxLength(3).IsRequired();
        b.Property(x => x.SaldoInicial).HasPrecision(18, 2);
        b.Property(x => x.SaldoActual).HasPrecision(18, 2);
        b.Property(x => x.EstadoCodigo).HasMaxLength(20).IsRequired();
        b.Property(x => x.CreatedBy).HasMaxLength(100);
        b.Property(x => x.UpdatedBy).HasMaxLength(100);

        b.HasOne(x => x.Empresa).WithMany().HasForeignKey(x => x.EmpresaId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.EmpresaId, x.Codigo }).IsUnique();
        b.HasIndex(x => new { x.EmpresaId, x.EstadoCodigo });
    }
}

public class MovimientoTesoreriaConfiguration : IEntityTypeConfiguration<MovimientoTesoreria>
{
    public void Configure(EntityTypeBuilder<MovimientoTesoreria> b)
    {
        b.ToTable("Tes_Movimientos");
        b.HasKey(x => x.Id);
        b.Property(x => x.Tipo).HasMaxLength(20).IsRequired();
        b.Property(x => x.Monto).HasPrecision(18, 2);
        b.Property(x => x.Concepto).HasMaxLength(200).IsRequired();
        b.Property(x => x.Referencia).HasMaxLength(80);
        b.Property(x => x.Origen).HasMaxLength(20).IsRequired();
        b.Property(x => x.SaldoResultante).HasPrecision(18, 2);
        b.Property(x => x.EstadoCodigo).HasMaxLength(20).IsRequired();
        b.Property(x => x.CreatedBy).HasMaxLength(100);
        b.Property(x => x.UpdatedBy).HasMaxLength(100);

        b.HasOne(x => x.Cuenta).WithMany(c => c.Movimientos).HasForeignKey(x => x.CuentaId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => new { x.EmpresaId, x.CuentaId, x.Fecha });
        b.HasIndex(x => new { x.EmpresaId, x.Origen, x.OrigenId });
    }
}

public class MovimientoBancarioConfiguration : IEntityTypeConfiguration<MovimientoBancario>
{
    public void Configure(EntityTypeBuilder<MovimientoBancario> b)
    {
        b.ToTable("Tes_MovimientosBanco");
        b.HasKey(x => x.Id);
        b.Property(x => x.Referencia).HasMaxLength(80);
        b.Property(x => x.Descripcion).HasMaxLength(200).IsRequired();
        b.Property(x => x.Monto).HasPrecision(18, 2);
        b.Property(x => x.EstadoCodigo).HasMaxLength(20).IsRequired();
        b.Property(x => x.ConciliadoPor).HasMaxLength(100);
        b.Property(x => x.CreatedBy).HasMaxLength(100);
        b.Property(x => x.UpdatedBy).HasMaxLength(100);

        b.HasOne(x => x.Cuenta).WithMany().HasForeignKey(x => x.CuentaTesoreriaId).OnDelete(DeleteBehavior.Cascade);
        // Restrict: SQL Server no admite otra ruta de cascada (banco→cuenta ya es CASCADE);
        // los movimientos internos se anulan, no se borran.
        b.HasOne(x => x.MovimientoTesoreria).WithMany().HasForeignKey(x => x.MovimientoTesoreriaId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.EmpresaId, x.CuentaTesoreriaId, x.EstadoCodigo });
        b.HasIndex(x => new { x.EmpresaId, x.CuentaTesoreriaId, x.Fecha });
    }
}
