using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NeoSTP.Domain.Core.Conta;

namespace NeoSTP.Infrastructure.Persistence.Configurations;

public class CuentaContableConfiguration : IEntityTypeConfiguration<CuentaContable>
{
    public void Configure(EntityTypeBuilder<CuentaContable> b)
    {
        b.ToTable("Conta_Cuentas");
        b.HasKey(x => x.Id);
        b.Property(x => x.Codigo).HasMaxLength(20).IsRequired();
        b.Property(x => x.Nombre).HasMaxLength(120).IsRequired();
        b.Property(x => x.Tipo).HasMaxLength(20).IsRequired();
        b.Property(x => x.CreatedBy).HasMaxLength(100);
        b.Property(x => x.UpdatedBy).HasMaxLength(100);

        b.HasOne(x => x.Empresa).WithMany().HasForeignKey(x => x.EmpresaId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.EmpresaId, x.Codigo }).IsUnique();
    }
}

public class AsientoContableConfiguration : IEntityTypeConfiguration<AsientoContable>
{
    public void Configure(EntityTypeBuilder<AsientoContable> b)
    {
        b.ToTable("Conta_Asientos");
        b.HasKey(x => x.Id);
        b.Property(x => x.Numero).HasMaxLength(30).IsRequired();
        b.Property(x => x.Concepto).HasMaxLength(300).IsRequired();
        b.Property(x => x.Origen).HasMaxLength(20).IsRequired();
        b.Property(x => x.EstadoCodigo).HasMaxLength(20).IsRequired();
        b.Property(x => x.TotalDebe).HasPrecision(18, 2);
        b.Property(x => x.TotalHaber).HasPrecision(18, 2);
        b.Property(x => x.CreatedBy).HasMaxLength(100);
        b.Property(x => x.UpdatedBy).HasMaxLength(100);

        b.HasOne(x => x.Empresa).WithMany().HasForeignKey(x => x.EmpresaId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.EmpresaId, x.Numero }).IsUnique();
        b.HasIndex(x => new { x.EmpresaId, x.Fecha });
        b.HasIndex(x => new { x.EmpresaId, x.Origen, x.OrigenId });
    }
}

public class AsientoContableLineaConfiguration : IEntityTypeConfiguration<AsientoContableLinea>
{
    public void Configure(EntityTypeBuilder<AsientoContableLinea> b)
    {
        b.ToTable("Conta_AsientoLineas");
        b.HasKey(x => x.Id);
        b.Property(x => x.Debe).HasPrecision(18, 2);
        b.Property(x => x.Haber).HasPrecision(18, 2);
        b.Property(x => x.Detalle).HasMaxLength(250);
        b.Property(x => x.CreatedBy).HasMaxLength(100);
        b.Property(x => x.UpdatedBy).HasMaxLength(100);

        b.HasOne(x => x.Asiento).WithMany(a => a.Lineas).HasForeignKey(x => x.AsientoContableId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Cuenta).WithMany().HasForeignKey(x => x.CuentaContableId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => x.AsientoContableId);
    }
}
