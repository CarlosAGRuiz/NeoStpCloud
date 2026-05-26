using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NeoSTP.Domain.Core.Licenciamiento;

namespace NeoSTP.Infrastructure.Persistence.Configurations;

public class EmpresaModuloConfiguration : IEntityTypeConfiguration<EmpresaModulo>
{
    public void Configure(EntityTypeBuilder<EmpresaModulo> builder)
    {
        builder.ToTable("Core_EmpresaModulos");
        builder.HasKey(em => new { em.EmpresaId, em.ModuloId });

        builder.HasOne(em => em.Empresa)
            .WithMany()
            .HasForeignKey(em => em.EmpresaId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(em => em.Modulo)
            .WithMany()
            .HasForeignKey(em => em.ModuloId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
