using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NeoSTP.Domain.Core.Empresas;

namespace NeoSTP.Infrastructure.Persistence.Configurations;

public class EmpresaConfiguration : IEntityTypeConfiguration<Empresa>
{
    public void Configure(EntityTypeBuilder<Empresa> builder)
    {
        builder.ToTable("Core_Empresas");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Nit).HasMaxLength(20).IsRequired();
        builder.Property(e => e.Nrc).HasMaxLength(20);
        builder.Property(e => e.RazonSocial).HasMaxLength(250).IsRequired();
        builder.Property(e => e.NombreComercial).HasMaxLength(250);
        builder.Property(e => e.CodigoActividad).HasMaxLength(20);
        builder.Property(e => e.ActividadEconomica).HasMaxLength(250);
        builder.Property(e => e.Departamento).HasMaxLength(100);
        builder.Property(e => e.Municipio).HasMaxLength(100);
        builder.Property(e => e.Direccion).HasMaxLength(500);
        builder.Property(e => e.Telefono).HasMaxLength(30);
        builder.Property(e => e.Correo).HasMaxLength(150);
        builder.Property(e => e.LogoUrl).HasMaxLength(500);
        builder.Property(e => e.EstadoCodigo).HasMaxLength(30).IsRequired();
        builder.Property(e => e.CreatedBy).HasMaxLength(100);
        builder.Property(e => e.UpdatedBy).HasMaxLength(100);

        builder.HasIndex(e => e.Nit).IsUnique();
        builder.HasIndex(e => e.EstadoCodigo);
    }
}
