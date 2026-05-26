using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NeoSTP.Domain.Core.Licenciamiento;

namespace NeoSTP.Infrastructure.Persistence.Configurations;

public class PlanModuloConfiguration : IEntityTypeConfiguration<PlanModulo>
{
    public void Configure(EntityTypeBuilder<PlanModulo> builder)
    {
        builder.ToTable("Core_PlanModulos");
        builder.HasKey(pm => new { pm.PlanId, pm.ModuloId });

        builder.HasOne(pm => pm.Plan)
            .WithMany(p => p.Modulos)
            .HasForeignKey(pm => pm.PlanId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(pm => pm.Modulo)
            .WithMany(m => m.Planes)
            .HasForeignKey(pm => pm.ModuloId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
