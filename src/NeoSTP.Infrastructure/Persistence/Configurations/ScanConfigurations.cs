using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NeoSTP.Domain.Core.Scan;

namespace NeoSTP.Infrastructure.Persistence.Configurations;

public class ScanDocumentoConfiguration : IEntityTypeConfiguration<ScanDocumento>
{
    public void Configure(EntityTypeBuilder<ScanDocumento> b)
    {
        b.ToTable("Scan_Documentos");
        b.HasKey(x => x.Id);
        b.Property(x => x.EstadoCodigo).HasMaxLength(20).IsRequired();
        b.Property(x => x.TipoClasificacion).HasMaxLength(20);
        b.Property(x => x.Origen).HasMaxLength(20).IsRequired();
        b.Property(x => x.ArchivoContentType).HasMaxLength(100);
        b.Property(x => x.ArchivoNombre).HasMaxLength(200);
        b.Property(x => x.EmisorNombre).HasMaxLength(250);
        b.Property(x => x.EmisorNit).HasMaxLength(30);
        b.Property(x => x.EmisorNrc).HasMaxLength(30);
        b.Property(x => x.TipoDocumento).HasMaxLength(60);
        b.Property(x => x.NumeroControl).HasMaxLength(60);
        b.Property(x => x.SelloRecibido).HasMaxLength(200);
        b.Property(x => x.Notas).HasMaxLength(500);
        b.Property(x => x.Subtotal).HasPrecision(18, 2);
        b.Property(x => x.Iva).HasPrecision(18, 2);
        b.Property(x => x.Total).HasPrecision(18, 2);
        b.Property(x => x.Confianza).HasPrecision(5, 4);
        b.Property(x => x.CreatedBy).HasMaxLength(100);
        b.Property(x => x.UpdatedBy).HasMaxLength(100);

        b.HasOne(x => x.Empresa).WithMany().HasForeignKey(x => x.EmpresaId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.EmpresaId, x.EstadoCodigo });
    }
}

public class DteDocumentoRecibidoConfiguration : IEntityTypeConfiguration<DteDocumentoRecibido>
{
    public void Configure(EntityTypeBuilder<DteDocumentoRecibido> b)
    {
        b.ToTable("Dte_DocumentosRecibidos");
        b.HasKey(x => x.Id);
        b.Property(x => x.EmisorNombre).HasMaxLength(250).IsRequired();
        b.Property(x => x.EmisorNit).HasMaxLength(30);
        b.Property(x => x.EmisorNrc).HasMaxLength(30);
        b.Property(x => x.TipoDteCodigo).HasMaxLength(10);
        b.Property(x => x.NumeroControl).HasMaxLength(60);
        b.Property(x => x.SelloRecibido).HasMaxLength(200);
        b.Property(x => x.Subtotal).HasPrecision(18, 2);
        b.Property(x => x.Iva).HasPrecision(18, 2);
        b.Property(x => x.Total).HasPrecision(18, 2);
        b.Property(x => x.CreatedBy).HasMaxLength(100);
        b.Property(x => x.UpdatedBy).HasMaxLength(100);

        b.HasOne(x => x.Empresa).WithMany().HasForeignKey(x => x.EmpresaId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.EmpresaId, x.Fecha });
    }
}
