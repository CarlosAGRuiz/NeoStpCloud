using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NeoSTP.Domain.Core.Seguridad;

namespace NeoSTP.Infrastructure.Persistence.Configurations;

public class EmpresaSsoConfiguration : IEntityTypeConfiguration<EmpresaSso>
{
    public void Configure(EntityTypeBuilder<EmpresaSso> b)
    {
        b.ToTable("Core_EmpresaSso");
        b.HasKey(x => x.Id);
        b.Property(x => x.ProveedorCodigo).HasMaxLength(20).IsRequired();
        b.Property(x => x.DominioCorreo).HasMaxLength(200).IsRequired();
        b.Property(x => x.TenantIdExterno).HasMaxLength(100);
        b.Property(x => x.Notas).HasMaxLength(500);
        b.Property(x => x.CreatedBy).HasMaxLength(100);
        b.Property(x => x.UpdatedBy).HasMaxLength(100);

        b.HasOne(x => x.Empresa).WithMany().HasForeignKey(x => x.EmpresaId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.RolPorDefecto).WithMany().HasForeignKey(x => x.RolPorDefectoId).OnDelete(DeleteBehavior.Restrict);

        // Una configuración SSO por empresa; el dominio resuelve empresa en el login federado.
        b.HasIndex(x => x.EmpresaId).IsUnique();
        b.HasIndex(x => x.DominioCorreo);
    }
}
