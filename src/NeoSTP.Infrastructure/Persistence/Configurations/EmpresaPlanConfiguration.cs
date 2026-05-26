using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NeoSTP.Domain.Core.Licenciamiento;

namespace NeoSTP.Infrastructure.Persistence.Configurations;

public class EmpresaPlanConfiguration : IEntityTypeConfiguration<EmpresaPlan>
{
    public void Configure(EntityTypeBuilder<EmpresaPlan> builder)
    {
        builder.ToTable("Core_EmpresaPlan");
        builder.HasKey(ep => ep.Id);

        builder.Property(ep => ep.EstadoCodigo).HasMaxLength(30).IsRequired();
        builder.Property(ep => ep.CreatedBy).HasMaxLength(100);
        builder.Property(ep => ep.UpdatedBy).HasMaxLength(100);

        builder.HasIndex(ep => new { ep.EmpresaId, ep.EstadoCodigo });
        builder.HasIndex(ep => ep.FechaFin);

        builder.HasOne(ep => ep.Empresa)
            .WithMany()
            .HasForeignKey(ep => ep.EmpresaId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(ep => ep.Plan)
            .WithMany()
            .HasForeignKey(ep => ep.PlanId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
