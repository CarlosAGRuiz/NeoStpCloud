using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NeoSTP.Domain.Core.Dte.Eventos;

namespace NeoSTP.Infrastructure.Persistence.Configurations;

public class DteEventoRespuestaHaciendaConfiguration : IEntityTypeConfiguration<DteEventoRespuestaHacienda>
{
    public void Configure(EntityTypeBuilder<DteEventoRespuestaHacienda> builder)
    {
        builder.ToTable("Dte_EventoRespuestasHacienda");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.RespuestaCrudaJson).HasColumnType("nvarchar(max)").IsRequired();
        builder.Property(r => r.Estado).HasMaxLength(30);
        builder.Property(r => r.CodigoMsg).HasMaxLength(30);
        builder.Property(r => r.DescripcionMsg).HasMaxLength(500);
        builder.Property(r => r.SelloRecibido).HasMaxLength(100);
        builder.Property(r => r.CreatedBy).HasMaxLength(100);
        builder.Property(r => r.UpdatedBy).HasMaxLength(100);

        builder.HasIndex(r => new { r.EventoId, r.RecibidoAt });
        builder.HasIndex(r => r.CodigoMsg);
    }
}
