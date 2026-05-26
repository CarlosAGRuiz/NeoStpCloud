using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NeoSTP.Domain.Core.Catalogos;

namespace NeoSTP.Infrastructure.Persistence.Configurations;

public class CatalogoConfiguration : IEntityTypeConfiguration<Catalogo>
{
    public void Configure(EntityTypeBuilder<Catalogo> builder)
    {
        builder.ToTable("Core_Catalogos");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Codigo).HasMaxLength(50).IsRequired();
        builder.Property(c => c.Nombre).HasMaxLength(150).IsRequired();
        builder.Property(c => c.Descripcion).HasMaxLength(500);
        builder.Property(c => c.CreatedBy).HasMaxLength(100);
        builder.Property(c => c.UpdatedBy).HasMaxLength(100);

        builder.HasIndex(c => new { c.Codigo, c.EmpresaId }).IsUnique();
        builder.HasIndex(c => c.EmpresaId);

        builder.HasMany(c => c.Items)
            .WithOne(i => i.Catalogo)
            .HasForeignKey(i => i.CatalogoId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
