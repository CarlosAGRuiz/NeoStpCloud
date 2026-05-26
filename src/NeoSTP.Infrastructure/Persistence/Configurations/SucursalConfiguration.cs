using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NeoSTP.Domain.Core.Empresas;

namespace NeoSTP.Infrastructure.Persistence.Configurations;

public class SucursalConfiguration : IEntityTypeConfiguration<Sucursal>
{
    public void Configure(EntityTypeBuilder<Sucursal> builder)
    {
        builder.ToTable("Core_Sucursales");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Codigo).HasMaxLength(20).IsRequired();
        builder.Property(s => s.Nombre).HasMaxLength(150).IsRequired();
        builder.Property(s => s.CodigoEstablecimientoMh).HasMaxLength(20);
        builder.Property(s => s.TipoEstablecimientoCodigo).HasMaxLength(20);
        builder.Property(s => s.Direccion).HasMaxLength(500);
        builder.Property(s => s.Telefono).HasMaxLength(30);
        builder.Property(s => s.Departamento).HasMaxLength(100);
        builder.Property(s => s.Municipio).HasMaxLength(100);
        builder.Property(s => s.EstadoCodigo).HasMaxLength(30).IsRequired();
        builder.Property(s => s.CreatedBy).HasMaxLength(100);
        builder.Property(s => s.UpdatedBy).HasMaxLength(100);

        builder.HasIndex(s => new { s.EmpresaId, s.Codigo }).IsUnique();

        builder.HasOne(s => s.Empresa)
            .WithMany(e => e.Sucursales)
            .HasForeignKey(s => s.EmpresaId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
