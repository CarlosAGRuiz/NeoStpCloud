using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NeoSTP.Domain.Core.Clientes;

namespace NeoSTP.Infrastructure.Persistence.Configurations;

public class ClienteConfiguration : IEntityTypeConfiguration<Cliente>
{
    public void Configure(EntityTypeBuilder<Cliente> builder)
    {
        builder.ToTable("Dte_Clientes");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.TipoDocumentoCodigo).HasMaxLength(30).IsRequired();
        builder.Property(c => c.NumeroDocumento).HasMaxLength(50).IsRequired();
        builder.Property(c => c.Nrc).HasMaxLength(20);
        builder.Property(c => c.Nombre).HasMaxLength(250).IsRequired();
        builder.Property(c => c.NombreComercial).HasMaxLength(250);
        builder.Property(c => c.TipoContribuyenteCodigo).HasMaxLength(30).IsRequired();
        builder.Property(c => c.CodigoActividad).HasMaxLength(20);
        builder.Property(c => c.ActividadEconomica).HasMaxLength(250);
        builder.Property(c => c.DepartamentoCodigo).HasMaxLength(20);
        builder.Property(c => c.MunicipioCodigo).HasMaxLength(20);
        builder.Property(c => c.Direccion).HasMaxLength(500);
        builder.Property(c => c.Correo).HasMaxLength(150);
        builder.Property(c => c.Telefono).HasMaxLength(30);
        builder.Property(c => c.EstadoCodigo).HasMaxLength(30).IsRequired();
        builder.Property(c => c.CreatedBy).HasMaxLength(100);
        builder.Property(c => c.UpdatedBy).HasMaxLength(100);

        builder.Ignore(c => c.EsContribuyente);

        builder.HasIndex(c => new { c.EmpresaId, c.TipoDocumentoCodigo, c.NumeroDocumento }).IsUnique();
        builder.HasIndex(c => new { c.EmpresaId, c.Nombre });
        builder.HasIndex(c => c.EstadoCodigo);

        builder.HasOne(c => c.Empresa)
            .WithMany()
            .HasForeignKey(c => c.EmpresaId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
