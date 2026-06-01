using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NeoSTP.Domain.Core.Ops;

namespace NeoSTP.Infrastructure.Persistence.Configurations;

public class BackupJobConfiguration : IEntityTypeConfiguration<BackupJob>
{
    public void Configure(EntityTypeBuilder<BackupJob> b)
    {
        b.ToTable("Ops_BackupJobs");
        b.HasKey(x => x.Id);
        b.Property(x => x.TipoBackup).HasMaxLength(20).IsRequired();
        b.Property(x => x.EstadoCodigo).HasMaxLength(20).IsRequired();
        b.Property(x => x.Origen).HasMaxLength(20).IsRequired();
        b.Property(x => x.StorageProvider).HasMaxLength(20).IsRequired();
        b.Property(x => x.StoragePath).HasMaxLength(500);
        b.Property(x => x.Checksum).HasMaxLength(128);
        b.Property(x => x.Error).HasMaxLength(2000);
        b.Property(x => x.CreatedBy).HasMaxLength(100);
        b.Property(x => x.UpdatedBy).HasMaxLength(100);

        b.HasIndex(x => new { x.EstadoCodigo, x.IniciadoAt });
        b.HasIndex(x => x.EmpresaId);
    }
}

public class ApiUsageLogConfiguration : IEntityTypeConfiguration<ApiUsageLog>
{
    public void Configure(EntityTypeBuilder<ApiUsageLog> b)
    {
        b.ToTable("Core_ApiUsageLog");
        b.HasKey(x => x.Id);
        b.Property(x => x.Metodo).HasMaxLength(10).IsRequired();
        b.Property(x => x.Ruta).HasMaxLength(500).IsRequired();
        b.Property(x => x.Modulo).HasMaxLength(30);
        b.Property(x => x.IpOrigen).HasMaxLength(64);
        b.Property(x => x.CreatedBy).HasMaxLength(100);
        b.Property(x => x.UpdatedBy).HasMaxLength(100);

        // Conteo de cuotas por ventana: filtra por sujeto + tiempo
        b.HasIndex(x => new { x.EmpresaId, x.OcurrioAt });
        b.HasIndex(x => new { x.UsuarioId, x.OcurrioAt });
        b.HasIndex(x => new { x.ApiKeyId, x.OcurrioAt });
    }
}

public class ApiQuotaConfiguration : IEntityTypeConfiguration<ApiQuota>
{
    public void Configure(EntityTypeBuilder<ApiQuota> b)
    {
        b.ToTable("Core_ApiQuotas");
        b.HasKey(x => x.Id);
        b.Property(x => x.Ambito).HasMaxLength(20).IsRequired();
        b.Property(x => x.AmbitoRef).HasMaxLength(100);
        b.Property(x => x.Descripcion).HasMaxLength(300);
        b.Property(x => x.CreatedBy).HasMaxLength(100);
        b.Property(x => x.UpdatedBy).HasMaxLength(100);

        b.HasIndex(x => new { x.EmpresaId, x.Ambito, x.AmbitoRef, x.Activo });
    }
}

public class AdminIpAllowlistEntryConfiguration : IEntityTypeConfiguration<AdminIpAllowlistEntry>
{
    public void Configure(EntityTypeBuilder<AdminIpAllowlistEntry> b)
    {
        b.ToTable("Core_AdminIpAllowlist");
        b.HasKey(x => x.Id);
        b.Property(x => x.IpCidr).HasMaxLength(64).IsRequired();
        b.Property(x => x.Descripcion).HasMaxLength(300);
        b.Property(x => x.CreatedBy).HasMaxLength(100);
        b.Property(x => x.UpdatedBy).HasMaxLength(100);

        b.HasIndex(x => x.Activo);
        b.HasIndex(x => x.IpCidr).IsUnique();
    }
}
