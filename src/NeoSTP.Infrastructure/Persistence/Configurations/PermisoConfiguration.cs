using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NeoSTP.Domain.Core.Seguridad;

namespace NeoSTP.Infrastructure.Persistence.Configurations;

public class PermisoConfiguration : IEntityTypeConfiguration<Permiso>
{
    public void Configure(EntityTypeBuilder<Permiso> builder)
    {
        builder.ToTable("Core_Permisos");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Codigo).HasMaxLength(100).IsRequired();
        builder.Property(p => p.Modulo).HasMaxLength(50).IsRequired();
        builder.Property(p => p.Descripcion).HasMaxLength(500);
        builder.Property(p => p.CreatedBy).HasMaxLength(100);
        builder.Property(p => p.UpdatedBy).HasMaxLength(100);

        builder.HasIndex(p => p.Codigo).IsUnique();
        builder.HasIndex(p => p.Modulo);
    }
}
