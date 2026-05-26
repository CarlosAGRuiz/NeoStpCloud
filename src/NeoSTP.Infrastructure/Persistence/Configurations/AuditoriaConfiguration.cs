using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NeoSTP.Domain.Core.Auditoria;

namespace NeoSTP.Infrastructure.Persistence.Configurations;

public class AuditoriaConfiguration : IEntityTypeConfiguration<Auditoria>
{
    public void Configure(EntityTypeBuilder<Auditoria> builder)
    {
        builder.ToTable("Core_Auditoria");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Username).HasMaxLength(100);
        builder.Property(a => a.Modulo).HasMaxLength(50).IsRequired();
        builder.Property(a => a.Accion).HasMaxLength(100).IsRequired();
        builder.Property(a => a.Entidad).HasMaxLength(100);
        builder.Property(a => a.EntidadId).HasMaxLength(100);
        builder.Property(a => a.DatosAntes).HasColumnType("nvarchar(max)");
        builder.Property(a => a.DatosDespues).HasColumnType("nvarchar(max)");
        builder.Property(a => a.Resultado).HasMaxLength(20).IsRequired();
        builder.Property(a => a.Detalle).HasMaxLength(2000);
        builder.Property(a => a.IpAddress).HasMaxLength(45);
        builder.Property(a => a.UserAgent).HasMaxLength(500);
        builder.Property(a => a.TraceId).HasMaxLength(100);

        builder.HasIndex(a => new { a.EmpresaId, a.CreatedAt });
        builder.HasIndex(a => new { a.UsuarioId, a.CreatedAt });
        builder.HasIndex(a => new { a.Modulo, a.Accion });
        builder.HasIndex(a => a.TraceId);
    }
}
