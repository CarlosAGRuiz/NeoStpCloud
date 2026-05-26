using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NeoSTP.Domain.Core.Licenciamiento;

namespace NeoSTP.Infrastructure.Persistence.Configurations;

public class ModuloConfiguration : IEntityTypeConfiguration<Modulo>
{
    public void Configure(EntityTypeBuilder<Modulo> builder)
    {
        builder.ToTable("Core_Modulos");
        builder.HasKey(m => m.Id);

        builder.Property(m => m.Codigo).HasMaxLength(50).IsRequired();
        builder.Property(m => m.Nombre).HasMaxLength(150).IsRequired();
        builder.Property(m => m.Descripcion).HasMaxLength(500);
        builder.Property(m => m.Icono).HasMaxLength(100);
        builder.Property(m => m.CreatedBy).HasMaxLength(100);
        builder.Property(m => m.UpdatedBy).HasMaxLength(100);

        builder.HasIndex(m => m.Codigo).IsUnique();
    }
}
