using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NeoSTP.Domain.Core.Comunicaciones;

namespace NeoSTP.Infrastructure.Persistence.Configurations;

public class ConfiguracionCorreoConfiguration : IEntityTypeConfiguration<ConfiguracionCorreo>
{
    public void Configure(EntityTypeBuilder<ConfiguracionCorreo> b)
    {
        b.ToTable("Com_ConfiguracionCorreo");
        b.HasKey(x => x.Id);
        b.Property(x => x.Host).HasMaxLength(160).IsRequired();
        b.Property(x => x.Usuario).HasMaxLength(160);
        b.Property(x => x.PasswordProtegida).HasMaxLength(1024);
        b.Property(x => x.FromNombre).HasMaxLength(120).IsRequired();
        b.Property(x => x.FromEmail).HasMaxLength(160).IsRequired();
        b.Property(x => x.CreatedBy).HasMaxLength(100);
        b.Property(x => x.UpdatedBy).HasMaxLength(100);

        b.HasOne(x => x.Empresa).WithMany().HasForeignKey(x => x.EmpresaId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => x.EmpresaId).IsUnique();
    }
}
