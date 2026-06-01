using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NeoSTP.Domain.Core.Dte.Eventos;

namespace NeoSTP.Infrastructure.Persistence.Configurations;

public class DteEventoDocumentoRelacionadoConfiguration : IEntityTypeConfiguration<DteEventoDocumentoRelacionado>
{
    public void Configure(EntityTypeBuilder<DteEventoDocumentoRelacionado> builder)
    {
        builder.ToTable("Dte_EventoDocumentosRelacionados");
        builder.HasKey(d => d.Id);

        builder.Property(d => d.RolCodigo).HasMaxLength(30).IsRequired();
        builder.Property(d => d.NumeroControlSnapshot).HasMaxLength(40);
        builder.Property(d => d.CreatedBy).HasMaxLength(100);
        builder.Property(d => d.UpdatedBy).HasMaxLength(100);

        // Un DTE puede aparecer solo una vez por evento (no se duplica dentro de un mismo lote/anulación).
        builder.HasIndex(d => new { d.EventoId, d.DocumentoId }).IsUnique();
        builder.HasIndex(d => d.DocumentoId);

        builder.HasOne(d => d.Documento)
            .WithMany()
            .HasForeignKey(d => d.DocumentoId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
