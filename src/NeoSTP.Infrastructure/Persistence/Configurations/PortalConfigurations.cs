using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NeoSTP.Domain.Core.Portal;

namespace NeoSTP.Infrastructure.Persistence.Configurations;

public class PortalAccesoConfiguration : IEntityTypeConfiguration<PortalAcceso>
{
    public void Configure(EntityTypeBuilder<PortalAcceso> b)
    {
        b.ToTable("Portal_Accesos");
        b.HasKey(x => x.Id);
        b.Property(x => x.Tipo).HasMaxLength(20).IsRequired();
        b.Property(x => x.TokenHash).HasMaxLength(64).IsRequired();
        b.Property(x => x.Nota).HasMaxLength(200);
        b.Property(x => x.CreatedBy).HasMaxLength(100);
        b.Property(x => x.UpdatedBy).HasMaxLength(100);

        b.HasOne(x => x.Empresa).WithMany().HasForeignKey(x => x.EmpresaId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => x.TokenHash).IsUnique();
        b.HasIndex(x => new { x.EmpresaId, x.Tipo });
    }
}
