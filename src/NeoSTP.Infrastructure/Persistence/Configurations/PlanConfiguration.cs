using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NeoSTP.Domain.Core.Licenciamiento;

namespace NeoSTP.Infrastructure.Persistence.Configurations;

public class PlanConfiguration : IEntityTypeConfiguration<Plan>
{
    public void Configure(EntityTypeBuilder<Plan> builder)
    {
        builder.ToTable("Core_Planes");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Codigo).HasMaxLength(50).IsRequired();
        builder.Property(p => p.Nombre).HasMaxLength(150).IsRequired();
        builder.Property(p => p.Descripcion).HasMaxLength(500);
        builder.Property(p => p.PrecioMensual).HasPrecision(18, 2);
        builder.Property(p => p.MonedaCodigo).HasMaxLength(10).IsRequired();
        builder.Property(p => p.CreatedBy).HasMaxLength(100);
        builder.Property(p => p.UpdatedBy).HasMaxLength(100);

        builder.HasIndex(p => p.Codigo).IsUnique();
    }
}
