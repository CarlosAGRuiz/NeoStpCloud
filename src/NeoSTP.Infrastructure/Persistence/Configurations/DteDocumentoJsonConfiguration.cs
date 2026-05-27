using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NeoSTP.Domain.Core.Dte;

namespace NeoSTP.Infrastructure.Persistence.Configurations;

public class DteDocumentoJsonConfiguration : IEntityTypeConfiguration<DteDocumentoJson>
{
    public void Configure(EntityTypeBuilder<DteDocumentoJson> builder)
    {
        builder.ToTable("Dte_DocumentoJson");
        builder.HasKey(d => d.Id);

        builder.Property(d => d.JsonDte).HasColumnType("nvarchar(max)").IsRequired();
        builder.Property(d => d.JsonFirmado).HasColumnType("nvarchar(max)");
        builder.Property(d => d.RespuestaHacienda).HasColumnType("nvarchar(max)");

        builder.Property(d => d.CreatedBy).HasMaxLength(100);
        builder.Property(d => d.UpdatedBy).HasMaxLength(100);

        builder.HasIndex(d => d.DocumentoId).IsUnique();
    }
}
