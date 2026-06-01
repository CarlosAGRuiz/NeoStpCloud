using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NeoSTP.Domain.Core.Dte.Eventos;

namespace NeoSTP.Infrastructure.Persistence.Configurations;

public class DteEventoJsonConfiguration : IEntityTypeConfiguration<DteEventoJson>
{
    public void Configure(EntityTypeBuilder<DteEventoJson> builder)
    {
        builder.ToTable("Dte_EventoJson");
        builder.HasKey(j => j.Id);

        builder.Property(j => j.JsonSinFirmar).HasColumnType("nvarchar(max)").IsRequired();
        builder.Property(j => j.JwsFirmado).HasColumnType("nvarchar(max)");
        builder.Property(j => j.CreatedBy).HasMaxLength(100);
        builder.Property(j => j.UpdatedBy).HasMaxLength(100);

        builder.HasIndex(j => j.EventoId).IsUnique();
    }
}
