using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NeoSTP.Domain.Core.Catalogos;

namespace NeoSTP.Infrastructure.Persistence.Configurations;

public class CatalogoItemConfiguration : IEntityTypeConfiguration<CatalogoItem>
{
    public void Configure(EntityTypeBuilder<CatalogoItem> builder)
    {
        builder.ToTable("Core_CatalogoItems");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Codigo).HasMaxLength(50).IsRequired();
        builder.Property(c => c.Valor).HasMaxLength(250).IsRequired();
        builder.Property(c => c.Descripcion).HasMaxLength(500);
        builder.Property(c => c.ParentCodigo).HasMaxLength(50);
        builder.Property(c => c.MetadataJson).HasColumnType("nvarchar(max)");
        builder.Property(c => c.CreatedBy).HasMaxLength(100);
        builder.Property(c => c.UpdatedBy).HasMaxLength(100);

        builder.HasIndex(c => new { c.CatalogoId, c.Codigo }).IsUnique();
        builder.HasIndex(c => new { c.CatalogoId, c.Activo, c.Orden });
        builder.HasIndex(c => new { c.CatalogoId, c.ParentCodigo });
    }
}
