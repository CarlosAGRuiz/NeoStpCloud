using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Cobranza.Dtos;
using NeoSTP.Application.Cobranza;
using NeoSTP.Application.Notificaciones;
using NeoSTP.Application.Notificaciones.Dtos;
using NeoSTP.Domain.Core.Dte;
using NeoSTP.Domain.Core.Empresas;
using NeoSTP.Domain.Core.Notificaciones;
using NeoSTP.Infrastructure.Persistence;
using NeoSTP.Infrastructure.Services;
using NSubstitute;

namespace NeoSTP.Tests.Integration;

/// <summary>
/// M5.1 — Integración Cobranza ↔ QR ↔ generación de alertas: una factura a crédito vencida
/// alimenta cobranza, QR de cobro y la generación de alertas, sobre un mismo DbContext.
/// </summary>
public class CobranzaAlertaIntegrationTests
{
    private const int Empresa = 200;
    private const int Usuario = 5;

    private static NeoStpDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<NeoStpDbContext>()
            .UseInMemoryDatabase($"cobranza-alerta-{Guid.NewGuid()}")
            .Options;
        var db = new NeoStpDbContext(options);
        db.Empresas.Add(new Empresa { Id = Empresa, Nit = "X", RazonSocial = "X", EstadoCodigo = "ACTIVA" });
        db.SaveChanges();
        return db;
    }

    /// <summary>Factura CCF a crédito, PROCESADA y vencida (emitida hace 60 días, plazo 30).</summary>
    private static DteDocumento FacturaCreditoVencida(decimal total = 113m) => new()
    {
        EmpresaId = Empresa,
        TipoDteCodigo = TipoDteCodigos.ComprobanteCreditoFiscal,
        EstadoCodigo = DteEstadoCodigos.Procesado,
        CondicionOperacionCodigo = "2", // crédito
        PlazoDias = 30,
        FechaEmision = DateTime.UtcNow.Date.AddDays(-60),
        NumeroControl = "DTE-03-0001",
        CodigoGeneracion = Guid.NewGuid().ToString().ToUpperInvariant(),
        ReceptorNombre = "Cliente Moroso SA",
        TotalPagar = total,
    };

    [Fact]
    public async Task FacturaCreditoVencida_ApareceEnCobranza_YGeneraQrDelSaldo()
    {
        using var db = NewDb();
        db.DteDocumentos.Add(FacturaCreditoVencida());
        await db.SaveChangesAsync();

        var audit = Substitute.For<IAuditoriaService>();
        var cobranza = new CobranzaService(db, audit);
        var qr = new CobroQrService(db, audit);

        var resumen = await cobranza.GetResumenAsync(Empresa);
        resumen.FacturasPendientes.Should().Be(1);
        resumen.FacturasVencidas.Should().Be(1);
        resumen.TotalPendiente.Should().Be(113m);

        var dteId = (await db.DteDocumentos.FirstAsync()).Id;
        await qr.CrearCuentaAsync(Empresa, new CrearCuentaCobroRequest { Tipo = "TRANSFERENCIA", Nombre = "BAC" }, "tester");

        var qrResult = await qr.GenerarQrAsync(Empresa, new GenerarQrCobroRequest { DteDocumentoId = dteId });
        qrResult.IsSuccess.Should().BeTrue();
        qrResult.Value!.Monto.Should().Be(113m); // monto = saldo de la factura
        qrResult.Value.QrPngBase64.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task RegistrarPago_ReduceSaldoEnCobranza()
    {
        using var db = NewDb();
        db.DteDocumentos.Add(FacturaCreditoVencida());
        await db.SaveChangesAsync();
        var cobranza = new CobranzaService(db, Substitute.For<IAuditoriaService>());
        var dteId = (await db.DteDocumentos.FirstAsync()).Id;

        var pago = await cobranza.RegistrarPagoAsync(Empresa, dteId, new RegistrarPagoRequest { Monto = 113m, FormaPagoCodigo = "TRANSFERENCIA" }, "tester");
        pago.IsSuccess.Should().BeTrue();

        var resumen = await cobranza.GetResumenAsync(Empresa);
        resumen.TotalPendiente.Should().Be(0m);
        resumen.FacturasPendientes.Should().Be(0);
    }

    [Fact]
    public async Task GenerarAlertas_DesdeFacturaVencida_LaListaElCentro()
    {
        using var db = NewDb();
        db.DteDocumentos.Add(FacturaCreditoVencida());
        await db.SaveChangesAsync();

        var cobranza = new CobranzaService(db, Substitute.For<IAuditoriaService>());
        var alertas = new AlertaService(db, Substitute.For<IPushSender>(), NullLogger<AlertaService>.Instance);
        var generacion = new AlertaGeneracionService(db, alertas, cobranza);

        var creadas = await generacion.GenerarAsync(Empresa);
        creadas.Should().BeGreaterThan(0);

        var lista = await alertas.ListarAsync(Empresa, Usuario, new AlertaQuery());
        lista.Value!.Items.Should().Contain(a => a.TipoCodigo == AlertaTipos.FacturaVencida);
    }
}
