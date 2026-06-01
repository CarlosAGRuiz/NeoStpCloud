using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NeoSTP.Domain.Core.Dte.Certificacion;

namespace NeoSTP.Infrastructure.Persistence.Configurations;

public class CertificacionPruebaConfiguration : IEntityTypeConfiguration<CertificacionPrueba>
{
    public void Configure(EntityTypeBuilder<CertificacionPrueba> builder)
    {
        builder.ToTable("Dte_CertificacionPruebas");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.EstadoCodigo).HasMaxLength(20).IsRequired();
        builder.Property(p => p.SelloRecibido).HasMaxLength(100);
        builder.Property(p => p.Notas).HasMaxLength(1000);
        builder.Property(p => p.CreatedBy).HasMaxLength(100);
        builder.Property(p => p.UpdatedBy).HasMaxLength(100);

        // Una empresa puede tener varios intentos del mismo escenario; IntentoNumero los distingue.
        builder.HasIndex(p => new { p.EmpresaId, p.EscenarioId, p.IntentoNumero }).IsUnique();

        // Índice ancho para listados/dashboard (estado por empresa).
        builder.HasIndex(p => new { p.EmpresaId, p.EstadoCodigo });

        builder.HasOne(p => p.Escenario)
            .WithMany()
            .HasForeignKey(p => p.EscenarioId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(p => p.DteDocumento)
            .WithMany()
            .HasForeignKey(p => p.DteDocumentoId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(p => p.Evento)
            .WithMany()
            .HasForeignKey(p => p.EventoId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(p => p.EventoId);

        builder.HasMany(p => p.Errores)
            .WithOne(e => e.Prueba)
            .HasForeignKey(e => e.PruebaId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
