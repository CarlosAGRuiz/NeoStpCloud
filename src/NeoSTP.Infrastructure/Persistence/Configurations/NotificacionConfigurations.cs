using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NeoSTP.Domain.Core.Notificaciones;

namespace NeoSTP.Infrastructure.Persistence.Configurations;

public class AlertaConfiguration : IEntityTypeConfiguration<Alerta>
{
    public void Configure(EntityTypeBuilder<Alerta> b)
    {
        b.ToTable("Notif_Alertas");
        b.HasKey(x => x.Id);
        b.Property(x => x.TipoCodigo).HasMaxLength(40).IsRequired();
        b.Property(x => x.Severidad).HasMaxLength(20).IsRequired();
        b.Property(x => x.Titulo).HasMaxLength(160).IsRequired();
        b.Property(x => x.Mensaje).HasMaxLength(500).IsRequired();
        b.Property(x => x.EntidadTipo).HasMaxLength(60);
        b.Property(x => x.EstadoCodigo).HasMaxLength(20).IsRequired();
        b.Property(x => x.Clave).HasMaxLength(120).IsRequired();
        b.Property(x => x.CreatedBy).HasMaxLength(100);
        b.Property(x => x.UpdatedBy).HasMaxLength(100);

        b.HasOne(x => x.Empresa).WithMany().HasForeignKey(x => x.EmpresaId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.EmpresaId, x.EstadoCodigo });
        b.HasIndex(x => new { x.EmpresaId, x.Clave });
    }
}

public class DispositivoNotificacionConfiguration : IEntityTypeConfiguration<DispositivoNotificacion>
{
    public void Configure(EntityTypeBuilder<DispositivoNotificacion> b)
    {
        b.ToTable("Notif_Dispositivos");
        b.HasKey(x => x.Id);
        b.Property(x => x.Token).HasMaxLength(300).IsRequired();
        b.Property(x => x.Plataforma).HasMaxLength(20).IsRequired();
        b.Property(x => x.CreatedBy).HasMaxLength(100);
        b.Property(x => x.UpdatedBy).HasMaxLength(100);

        b.HasIndex(x => x.Token).IsUnique();
        b.HasIndex(x => new { x.EmpresaId, x.UsuarioId, x.Activo });
    }
}

public class PreferenciaNotificacionConfiguration : IEntityTypeConfiguration<PreferenciaNotificacion>
{
    public void Configure(EntityTypeBuilder<PreferenciaNotificacion> b)
    {
        b.ToTable("Notif_Preferencias");
        b.HasKey(x => x.Id);
        b.Property(x => x.Canal).HasMaxLength(20).IsRequired();
        b.Property(x => x.CreatedBy).HasMaxLength(100);
        b.Property(x => x.UpdatedBy).HasMaxLength(100);

        b.HasIndex(x => new { x.EmpresaId, x.UsuarioId }).IsUnique();
    }
}
