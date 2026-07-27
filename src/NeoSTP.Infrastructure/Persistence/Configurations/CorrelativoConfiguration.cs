using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NeoSTP.Domain.Core.Common;

namespace NeoSTP.Infrastructure.Persistence.Configurations;

public class CorrelativoConfiguration : IEntityTypeConfiguration<Correlativo>
{
    public void Configure(EntityTypeBuilder<Correlativo> b)
    {
        b.ToTable("Core_Correlativos");
        b.HasKey(x => new { x.EmpresaId, x.Serie });
        b.Property(x => x.Serie).HasMaxLength(20).IsRequired();
        b.Property(x => x.ActualizadoAt).IsRequired();
    }
}
