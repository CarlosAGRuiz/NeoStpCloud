using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NeoSTP.Domain.Core.Agenda;

namespace NeoSTP.Infrastructure.Persistence.Configurations;

public class CitaConfiguration : IEntityTypeConfiguration<Cita>
{
    public void Configure(EntityTypeBuilder<Cita> b)
    {
        b.ToTable("Agenda_Citas");
        b.HasKey(x => x.Id);
        b.Property(x => x.ClienteNombre).HasMaxLength(250).IsRequired();
        b.Property(x => x.EmpleadoNombre).HasMaxLength(200);
        b.Property(x => x.ServicioNombre).HasMaxLength(250).IsRequired();
        b.Property(x => x.Precio).HasPrecision(18, 2);
        b.Property(x => x.EstadoCodigo).HasMaxLength(20).IsRequired();
        b.Property(x => x.Nota).HasMaxLength(500);
        b.Property(x => x.CreatedBy).HasMaxLength(100);
        b.Property(x => x.UpdatedBy).HasMaxLength(100);
        b.Ignore(x => x.FechaFin);

        b.HasOne(x => x.Empresa).WithMany().HasForeignKey(x => x.EmpresaId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.EmpresaId, x.FechaInicio });
        b.HasIndex(x => new { x.EmpresaId, x.EmpleadoId, x.FechaInicio });
        b.HasIndex(x => new { x.EmpresaId, x.EstadoCodigo });
    }
}
