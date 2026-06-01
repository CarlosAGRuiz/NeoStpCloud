using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NeoSTP.Domain.Core.Dte.Contingencia;

namespace NeoSTP.Infrastructure.Persistence.Configurations;

public class DteContingenciaLoteConfiguration : IEntityTypeConfiguration<DteContingenciaLote>
{
    public void Configure(EntityTypeBuilder<DteContingenciaLote> builder)
    {
        builder.ToTable("Dte_ContingenciaLotes");
        builder.HasKey(l => l.Id);

        builder.Property(l => l.CodigoLote).HasMaxLength(50);
        builder.Property(l => l.SelloRecibido).HasMaxLength(100);
        builder.Property(l => l.EstadoCodigo).HasMaxLength(20).IsRequired();
        builder.Property(l => l.AmbienteCodigo).HasMaxLength(20).IsRequired();
        builder.Property(l => l.RawEnvio).HasColumnType("nvarchar(max)");
        builder.Property(l => l.RawConsulta).HasColumnType("nvarchar(max)");
        builder.Property(l => l.CreatedBy).HasMaxLength(100);
        builder.Property(l => l.UpdatedBy).HasMaxLength(100);

        // Índices para queries de dashboard y Worker
        builder.HasIndex(l => new { l.EmpresaId, l.EstadoCodigo });
        builder.HasIndex(l => l.EventoContingenciaId);

        builder.HasOne(l => l.Empresa)
            .WithMany()
            .HasForeignKey(l => l.EmpresaId)
            .OnDelete(DeleteBehavior.Restrict);

        // FK al DteEvento de contingencia (sin navegar para evitar ciclos)
        builder.HasMany(l => l.Detalles)
            .WithOne(d => d.Lote)
            .HasForeignKey(d => d.LoteId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class DteContingenciaLoteDetalleConfiguration : IEntityTypeConfiguration<DteContingenciaLoteDetalle>
{
    public void Configure(EntityTypeBuilder<DteContingenciaLoteDetalle> builder)
    {
        builder.ToTable("Dte_ContingenciaLoteDetalles");
        builder.HasKey(d => d.Id);

        builder.Property(d => d.CodigoGeneracion).HasMaxLength(36).IsRequired();
        builder.Property(d => d.TipoDteCodigo).HasMaxLength(5).IsRequired();
        builder.Property(d => d.SelloRecibido).HasMaxLength(100);
        builder.Property(d => d.EstadoCodigo).HasMaxLength(20).IsRequired();
        builder.Property(d => d.MensajeHacienda).HasMaxLength(500);
        builder.Property(d => d.CreatedBy).HasMaxLength(100);
        builder.Property(d => d.UpdatedBy).HasMaxLength(100);

        builder.HasIndex(d => new { d.LoteId, d.CodigoGeneracion });
        builder.HasIndex(d => d.DteDocumentoId);
    }
}
