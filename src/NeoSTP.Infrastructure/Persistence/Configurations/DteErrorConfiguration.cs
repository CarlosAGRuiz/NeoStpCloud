using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NeoSTP.Domain.Core.Dte.Diagnostico;

namespace NeoSTP.Infrastructure.Persistence.Configurations;

public class DteErrorCatalogoConfiguration : IEntityTypeConfiguration<DteErrorCatalogo>
{
    public void Configure(EntityTypeBuilder<DteErrorCatalogo> b)
    {
        b.ToTable("Dte_ErrorCatalogo");
        b.HasKey(x => x.Id);
        b.Property(x => x.Codigo).IsRequired().HasMaxLength(50);
        b.Property(x => x.Tipo).IsRequired().HasMaxLength(20);
        b.Property(x => x.MensajeTecnico).IsRequired().HasMaxLength(500);
        b.Property(x => x.Descripcion).IsRequired().HasMaxLength(500);
        b.Property(x => x.CausaProbable).IsRequired().HasMaxLength(500);
        b.Property(x => x.AccionSugerida).IsRequired().HasMaxLength(500);
        b.Property(x => x.Severidad).IsRequired().HasMaxLength(10);
        b.HasIndex(x => x.Codigo).IsUnique();
        b.HasIndex(x => x.Tipo);
    }
}

public class DteErrorOcurrenciaConfiguration : IEntityTypeConfiguration<DteErrorOcurrencia>
{
    public void Configure(EntityTypeBuilder<DteErrorOcurrencia> b)
    {
        b.ToTable("Dte_ErrorOcurrencias");
        b.HasKey(x => x.Id);
        b.Property(x => x.CodigoError).IsRequired().HasMaxLength(50);
        b.Property(x => x.Mensaje).IsRequired().HasMaxLength(1000);
        b.Property(x => x.Fuente).IsRequired().HasMaxLength(20);
        b.Property(x => x.RespuestaMhJson).HasColumnType("nvarchar(max)");
        b.Property(x => x.JsonEnviado).HasColumnType("nvarchar(max)");
        b.HasIndex(x => new { x.EmpresaId, x.CodigoError });
        b.HasIndex(x => new { x.EmpresaId, x.OcurrioAt });
        b.HasIndex(x => x.DteDocumentoId);
        b.HasIndex(x => x.DteEventoId);
    }
}
