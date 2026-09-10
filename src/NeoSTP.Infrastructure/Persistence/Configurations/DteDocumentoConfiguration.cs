using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NeoSTP.Domain.Core.Dte;

namespace NeoSTP.Infrastructure.Persistence.Configurations;

public class DteDocumentoConfiguration : IEntityTypeConfiguration<DteDocumento>
{
    public void Configure(EntityTypeBuilder<DteDocumento> builder)
    {
        builder.ToTable("Dte_Documentos");
        builder.HasKey(d => d.Id);
        builder.Property(d => d.IdempotencyScope).HasMaxLength(10);
        builder.Property(d => d.IdempotencyKeyHash).HasMaxLength(64).IsUnicode(false);
        builder.Property(d => d.IdempotencyRequestHash).HasMaxLength(64).IsUnicode(false);
        builder.HasIndex(d => new { d.EmpresaId, d.IdempotencyScope, d.IdempotencyKeyHash })
            .IsUnique().HasFilter("[IdempotencyKeyHash] IS NOT NULL");

        builder.Property(d => d.TipoDteCodigo).HasMaxLength(4).IsRequired();
        builder.Property(d => d.AmbienteCodigo).HasMaxLength(20).IsRequired();
        builder.Property(d => d.NumeroControl).HasMaxLength(40).IsRequired();
        builder.Property(d => d.CodigoGeneracion).HasMaxLength(36).IsRequired();
        builder.Property(d => d.SelloRecibido).HasMaxLength(100);

        builder.Property(d => d.TipoContingenciaCodigo).HasMaxLength(10);
        builder.Property(d => d.MotivoContingencia).HasMaxLength(500);

        builder.Property(d => d.TipoMonedaCodigo).HasMaxLength(10).IsRequired();
        builder.Property(d => d.CondicionOperacionCodigo).HasMaxLength(5).IsRequired();
        builder.Property(d => d.FormaPagoCodigo).HasMaxLength(50);
        builder.Property(d => d.Periodo).HasMaxLength(50);

        // Snapshot receptor
        builder.Property(d => d.ReceptorTipoDocumento).HasMaxLength(30);
        builder.Property(d => d.ReceptorNumeroDocumento).HasMaxLength(50);
        builder.Property(d => d.ReceptorNrc).HasMaxLength(20);
        builder.Property(d => d.ReceptorNombre).HasMaxLength(250);
        builder.Property(d => d.ReceptorTipoContribuyente).HasMaxLength(30);
        builder.Property(d => d.ReceptorCodigoActividad).HasMaxLength(20);
        builder.Property(d => d.ReceptorActividadEconomica).HasMaxLength(250);
        builder.Property(d => d.ReceptorDepartamentoCodigo).HasMaxLength(20);
        builder.Property(d => d.ReceptorMunicipioCodigo).HasMaxLength(20);
        builder.Property(d => d.ReceptorDireccion).HasMaxLength(500);
        builder.Property(d => d.ReceptorCorreo).HasMaxLength(150);
        builder.Property(d => d.ReceptorTelefono).HasMaxLength(30);
        builder.Property(d => d.ReceptorPaisCodigo).HasMaxLength(10);
        builder.Property(d => d.ReceptorPaisNombre).HasMaxLength(100);

        builder.Property(d => d.NumeroDocumentoRelacionado).HasMaxLength(40);
        builder.Property(d => d.TipoDteRelacionado).HasMaxLength(4);
        builder.Property(d => d.TipoGeneracionRelacionado).HasMaxLength(2);

        builder.Property(d => d.VentaTerceroNit).HasMaxLength(20);
        builder.Property(d => d.VentaTerceroNombre).HasMaxLength(250);

        builder.Property(d => d.Observaciones).HasMaxLength(1000);
        builder.Property(d => d.NotaInterna).HasMaxLength(2000);

        // Totales con precisión fiscal
        var monetaryProps = new[]
        {
            nameof(DteDocumento.TotalNoSujeto),
            nameof(DteDocumento.TotalExenta),
            nameof(DteDocumento.TotalGravada),
            nameof(DteDocumento.SubTotalVentas),
            nameof(DteDocumento.DescuentoNoSujeto),
            nameof(DteDocumento.DescuentoExenta),
            nameof(DteDocumento.DescuentoGravada),
            nameof(DteDocumento.PorcentajeDescuento),
            nameof(DteDocumento.TotalDescuento),
            nameof(DteDocumento.IvaTotal),
            nameof(DteDocumento.IvaRetenido),
            nameof(DteDocumento.ReteRenta),
            nameof(DteDocumento.SubTotal),
            nameof(DteDocumento.MontoTotalOperacion),
            nameof(DteDocumento.TotalNoGravado),
            nameof(DteDocumento.TotalPagar),
        };
        foreach (var p in monetaryProps)
            builder.Property(p).HasPrecision(18, 4);

        // Documento Contable de Liquidación (09): límites tomados del esquema fe-dcl-v2.
        builder.Property(d => d.LiquidacionCodigo).HasMaxLength(30);
        builder.Property(d => d.LiquidacionDescripcionSinPercepcion).HasMaxLength(100);
        builder.Property(d => d.LiquidacionNombreEntrega).HasMaxLength(100);
        builder.Property(d => d.LiquidacionDocumentoEntrega).HasMaxLength(25);
        builder.Property(d => d.LiquidacionCodigoEmpleado).HasMaxLength(15);
        foreach (var p in new[]
        {
            nameof(DteDocumento.LiquidacionMontoSinPercepcion),
            nameof(DteDocumento.LiquidacionIvaPercibido),
            nameof(DteDocumento.LiquidacionPorcentajeComision),
            nameof(DteDocumento.LiquidacionComision),
            nameof(DteDocumento.LiquidacionIvaComision),
        })
            builder.Property(p).HasPrecision(18, 4);

        builder.Property(d => d.TotalLetras).HasMaxLength(500);

        // Compare the persisted fiscal state/attempt on every update, across all hosts.
        // This turns the durable ENVIADO transition into an atomic claim and rejects stale writes.
        builder.Property(d => d.EstadoCodigo).HasMaxLength(30).IsRequired().IsConcurrencyToken();
        builder.Property(d => d.EnviadoAt).IsConcurrencyToken();
        // Generation revision prevents FIRMADO -> GENERADO -> FIRMADO ABA without
        // treating a harmless internal-note edit as a competing fiscal response.
        builder.Property(d => d.GeneradoAt).IsConcurrencyToken();
        builder.Property(d => d.CreatedBy).HasMaxLength(100);
        builder.Property(d => d.UpdatedBy).HasMaxLength(100);

        builder.Ignore(d => d.EsCreditoFiscal);
        builder.Ignore(d => d.EsNotaCredito);
        builder.Ignore(d => d.EsNotaDebito);
        builder.Ignore(d => d.EsSujetoExcluido);

        builder.HasIndex(d => new { d.EmpresaId, d.NumeroControl }).IsUnique();
        builder.HasIndex(d => d.CodigoGeneracion).IsUnique();
        builder.HasIndex(d => new { d.EmpresaId, d.TipoDteCodigo, d.FechaEmision });
        builder.HasIndex(d => new { d.EmpresaId, d.EstadoCodigo });

        builder.HasOne(d => d.Empresa)
            .WithMany()
            .HasForeignKey(d => d.EmpresaId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(d => d.Cliente)
            .WithMany()
            .HasForeignKey(d => d.ClienteId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(d => d.DocumentoRelacionado)
            .WithMany()
            .HasForeignKey(d => d.DocumentoRelacionadoId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(d => d.Detalles)
            .WithOne(x => x.Documento)
            .HasForeignKey(x => x.DocumentoId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(d => d.Json)
            .WithOne(x => x.Documento)
            .HasForeignKey<DteDocumentoJson>(x => x.DocumentoId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
