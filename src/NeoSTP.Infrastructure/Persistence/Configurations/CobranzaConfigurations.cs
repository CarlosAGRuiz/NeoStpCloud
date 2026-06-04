using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NeoSTP.Domain.Core.Cobranza;

namespace NeoSTP.Infrastructure.Persistence.Configurations;

public class PagoClienteConfiguration : IEntityTypeConfiguration<PagoCliente>
{
    public void Configure(EntityTypeBuilder<PagoCliente> b)
    {
        b.ToTable("Cobros_Pagos");
        b.HasKey(x => x.Id);
        b.Property(x => x.Monto).HasPrecision(18, 2);
        b.Property(x => x.FormaPagoCodigo).HasMaxLength(20).IsRequired();
        b.Property(x => x.Referencia).HasMaxLength(100);
        b.Property(x => x.Nota).HasMaxLength(300);
        b.Property(x => x.ComprobanteUrl).HasMaxLength(500);
        b.Property(x => x.EstadoCodigo).HasMaxLength(20).IsRequired();
        b.Property(x => x.CreatedBy).HasMaxLength(100);
        b.Property(x => x.UpdatedBy).HasMaxLength(100);

        b.HasOne(x => x.Empresa).WithMany().HasForeignKey(x => x.EmpresaId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.DteDocumento).WithMany().HasForeignKey(x => x.DteDocumentoId).OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(x => new { x.EmpresaId, x.DteDocumentoId });
        b.HasIndex(x => new { x.EmpresaId, x.EstadoCodigo });
    }
}

public class CuentaCobroConfiguration : IEntityTypeConfiguration<CuentaCobro>
{
    public void Configure(EntityTypeBuilder<CuentaCobro> b)
    {
        b.ToTable("Cobros_CuentasCobro");
        b.HasKey(x => x.Id);
        b.Property(x => x.Tipo).HasMaxLength(20).IsRequired();
        b.Property(x => x.Nombre).HasMaxLength(120).IsRequired();
        b.Property(x => x.Banco).HasMaxLength(120);
        b.Property(x => x.NumeroCuenta).HasMaxLength(60);
        b.Property(x => x.Titular).HasMaxLength(160);
        b.Property(x => x.UrlPago).HasMaxLength(500);
        b.Property(x => x.EstadoCodigo).HasMaxLength(20).IsRequired();
        b.Property(x => x.CreatedBy).HasMaxLength(100);
        b.Property(x => x.UpdatedBy).HasMaxLength(100);

        b.HasOne(x => x.Empresa).WithMany().HasForeignKey(x => x.EmpresaId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.EmpresaId, x.EstadoCodigo });
    }
}
