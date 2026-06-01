using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NeoSTP.Domain.Core.Dte.Eventos;

namespace NeoSTP.Infrastructure.Persistence.Configurations;

public class DteEventoConfiguration : IEntityTypeConfiguration<DteEvento>
{
    public void Configure(EntityTypeBuilder<DteEvento> builder)
    {
        builder.ToTable("Dte_Eventos");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.TipoEventoCodigo).HasMaxLength(30).IsRequired();
        builder.Property(e => e.CodigoGeneracion).HasMaxLength(36).IsRequired();
        builder.Property(e => e.AmbienteCodigo).HasMaxLength(20).IsRequired();
        builder.Property(e => e.EstadoCodigo).HasMaxLength(20).IsRequired();
        builder.Property(e => e.SelloRecibido).HasMaxLength(100);
        builder.Property(e => e.NumeroControlReferencia).HasMaxLength(40);
        builder.Property(e => e.MotivoLibre).HasMaxLength(500);
        builder.Property(e => e.CreatedBy).HasMaxLength(100);
        builder.Property(e => e.UpdatedBy).HasMaxLength(100);

        builder.HasIndex(e => e.CodigoGeneracion).IsUnique();
        builder.HasIndex(e => new { e.EmpresaId, e.FechaTransmision });
        builder.HasIndex(e => new { e.EmpresaId, e.TipoEventoCodigo, e.EstadoCodigo });

        builder.HasOne(e => e.Empresa)
            .WithMany()
            .HasForeignKey(e => e.EmpresaId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Json)
            .WithOne(j => j.Evento)
            .HasForeignKey<DteEventoJson>(j => j.EventoId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(e => e.Respuestas)
            .WithOne(r => r.Evento)
            .HasForeignKey(r => r.EventoId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(e => e.DocumentosRelacionados)
            .WithOne(d => d.Evento)
            .HasForeignKey(d => d.EventoId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
