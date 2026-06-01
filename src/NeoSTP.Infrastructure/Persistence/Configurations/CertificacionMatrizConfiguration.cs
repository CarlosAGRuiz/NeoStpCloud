using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NeoSTP.Domain.Core.Dte.Certificacion;

namespace NeoSTP.Infrastructure.Persistence.Configurations;

public class CertificacionMatrizConfiguration : IEntityTypeConfiguration<CertificacionMatriz>
{
    public void Configure(EntityTypeBuilder<CertificacionMatriz> builder)
    {
        builder.ToTable("Dte_CertificacionMatriz");
        builder.HasKey(m => m.Id);

        builder.Property(m => m.TipoDteCodigo).HasMaxLength(40).IsRequired();
        builder.Property(m => m.Nombre).HasMaxLength(150).IsRequired();
        builder.Property(m => m.Descripcion).HasMaxLength(500);
        builder.Property(m => m.CreatedBy).HasMaxLength(100);
        builder.Property(m => m.UpdatedBy).HasMaxLength(100);

        builder.HasIndex(m => m.TipoDteCodigo).IsUnique();

        builder.HasMany(m => m.Escenarios)
            .WithOne(e => e.Matriz)
            .HasForeignKey(e => e.MatrizId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
