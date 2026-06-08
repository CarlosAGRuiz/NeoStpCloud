using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NeoSTP.Domain.Core.Rrhh;

namespace NeoSTP.Infrastructure.Persistence.Configurations;

public class EmpleadoConfiguration : IEntityTypeConfiguration<Empleado>
{
    public void Configure(EntityTypeBuilder<Empleado> b)
    {
        b.ToTable("Rrhh_Empleados");
        b.HasKey(x => x.Id);
        b.Property(x => x.Codigo).HasMaxLength(30).IsRequired();
        b.Property(x => x.Nombres).HasMaxLength(120).IsRequired();
        b.Property(x => x.Apellidos).HasMaxLength(120).IsRequired();
        b.Property(x => x.TipoDocumento).HasMaxLength(20).IsRequired();
        b.Property(x => x.NumeroDocumento).HasMaxLength(30).IsRequired();
        b.Property(x => x.Nit).HasMaxLength(30);
        b.Property(x => x.IsssNumero).HasMaxLength(30);
        b.Property(x => x.AfpInstitucion).HasMaxLength(60);
        b.Property(x => x.AfpNumero).HasMaxLength(30);
        b.Property(x => x.Cargo).HasMaxLength(120);
        b.Property(x => x.Email).HasMaxLength(160);
        b.Property(x => x.Telefono).HasMaxLength(30);
        b.Property(x => x.EstadoCodigo).HasMaxLength(20).IsRequired();
        b.Property(x => x.CreatedBy).HasMaxLength(100);
        b.Property(x => x.UpdatedBy).HasMaxLength(100);
        b.Ignore(x => x.NombreCompleto);

        b.HasOne(x => x.Empresa).WithMany().HasForeignKey(x => x.EmpresaId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.EmpresaId, x.Codigo }).IsUnique();
        b.HasIndex(x => new { x.EmpresaId, x.EstadoCodigo });
    }
}

public class ContratoLaboralConfiguration : IEntityTypeConfiguration<ContratoLaboral>
{
    public void Configure(EntityTypeBuilder<ContratoLaboral> b)
    {
        b.ToTable("Rrhh_Contratos");
        b.HasKey(x => x.Id);
        b.Property(x => x.TipoContrato).HasMaxLength(20).IsRequired();
        b.Property(x => x.SalarioMensual).HasPrecision(18, 2);
        b.Property(x => x.PeriodicidadPago).HasMaxLength(20).IsRequired();
        b.Property(x => x.EstadoCodigo).HasMaxLength(20).IsRequired();
        b.Property(x => x.CreatedBy).HasMaxLength(100);
        b.Property(x => x.UpdatedBy).HasMaxLength(100);

        b.HasOne(x => x.Empresa).WithMany().HasForeignKey(x => x.EmpresaId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Empleado).WithMany(e => e.Contratos).HasForeignKey(x => x.EmpleadoId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => new { x.EmpleadoId, x.EstadoCodigo });
    }
}
