using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NeoSTP.Domain.Core.Dte;

namespace NeoSTP.Infrastructure.Persistence.Configurations;

public class DteCorrelativoConfiguration : IEntityTypeConfiguration<DteCorrelativo>
{
    public void Configure(EntityTypeBuilder<DteCorrelativo> builder)
    {
        builder.ToTable("Dte_Correlativos");
        builder.HasKey(x => new { x.EmpresaId, x.TipoDteCodigo });

        builder.Property(x => x.TipoDteCodigo).HasMaxLength(2).IsRequired();
        builder.Property(x => x.UltimoCorrelativo).IsRequired();
        builder.Property(x => x.ActualizadoAt).IsRequired();
    }
}
