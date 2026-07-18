using System.Text;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Cobranza;
using NeoSTP.Domain.Core.Cobranza;
using NeoSTP.Domain.Core.Dte;
using NeoSTP.Domain.Core.Empresas;
using NeoSTP.Infrastructure.Persistence;
using NeoSTP.Infrastructure.Services;
using NSubstitute;
using Xunit;

namespace NeoSTP.Tests.Unit.Cobranza;

/// <summary>
/// B-5: cuentas de cobro y generación de QR de pago (monto fijo o derivado del saldo de una factura).
/// </summary>
public class CobroQrServiceTests
{
    private const int EmpresaA = 60;
    private const int EmpresaB = 61;

    private static NeoStpDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<NeoStpDbContext>()
            .UseInMemoryDatabase($"cobroqr-{Guid.NewGuid()}")
            .Options;
        var db = new NeoStpDbContext(options);
        db.Empresas.Add(new Empresa { Id = EmpresaA, Nit = "A", RazonSocial = "A", EstadoCodigo = "ACTIVA" });
        db.Empresas.Add(new Empresa { Id = EmpresaB, Nit = "B", RazonSocial = "B", EstadoCodigo = "ACTIVA" });
        db.SaveChanges();
        return db;
    }

    private static CobroQrService NewSvc(NeoStpDbContext db)
        => new(db, Substitute.For<IAuditoriaService>());

    private static CrearCuentaCobroRequest Cuenta(string? url = null) => new()
    {
        Tipo = "TRANSFERENCIA", Nombre = "Cuenta principal", Banco = "Banco X",
        NumeroCuenta = "1234567890", Titular = "Mi Empresa", UrlPago = url,
    };

    [Fact]
    public async Task CrearYListarCuentas()
    {
        var db = NewDb();
        var svc = NewSvc(db);

        (await svc.CrearCuentaAsync(EmpresaA, Cuenta(), "tester")).IsSuccess.Should().BeTrue();
        (await svc.ListarCuentasAsync(EmpresaA)).Should().ContainSingle();
        (await svc.ListarCuentasAsync(EmpresaB)).Should().BeEmpty(); // aislamiento
    }

    [Fact]
    public async Task GenerarQr_MontoFijo_ProducePayloadYPng()
    {
        var db = NewDb();
        var svc = NewSvc(db);
        await svc.CrearCuentaAsync(EmpresaA, Cuenta(), "tester");

        var r = await svc.GenerarQrAsync(EmpresaA, new GenerarQrCobroRequest { Monto = 150m, Referencia = "FAC-1" });

        r.IsSuccess.Should().BeTrue();
        r.Value!.Monto.Should().Be(150m);
        r.Value.Referencia.Should().Be("FAC-1");
        r.Value.Payload.Should().Contain("150.00");
        var png = Convert.FromBase64String(r.Value.QrPngBase64);
        Encoding.ASCII.GetString(png, 1, 3).Should().Be("PNG");
    }

    [Fact]
    public async Task GenerarQr_ConUrlPago_SustituyeMarcadores()
    {
        var db = NewDb();
        var svc = NewSvc(db);
        await svc.CrearCuentaAsync(EmpresaA, Cuenta(url: "https://pago.sv/checkout?amount={monto}&ref={referencia}"), "tester");

        var r = await svc.GenerarQrAsync(EmpresaA, new GenerarQrCobroRequest { Monto = 99.5m, Referencia = "ABC 1" });

        r.Value!.Payload.Should().Be("https://pago.sv/checkout?amount=99.50&ref=ABC%201");
    }

    [Fact]
    public async Task GenerarQr_DesdeFactura_UsaSaldoYNumeroControl()
    {
        var db = NewDb();
        db.DteDocumentos.Add(new DteDocumento
        {
            Id = 1, EmpresaId = EmpresaA, TipoDteCodigo = "01", EstadoCodigo = DteEstadoCodigos.Procesado,
            CondicionOperacionCodigo = "2", TotalPagar = 200m, NumeroControl = "DTE-01-000999",
            CodigoGeneracion = Guid.NewGuid().ToString(),
        });
        db.PagosCliente.Add(new PagoCliente { EmpresaId = EmpresaA, DteDocumentoId = 1, Monto = 50m, EstadoCodigo = PagoEstados.Confirmado, Fecha = DateOnly.FromDateTime(DateTime.UtcNow) });
        await db.SaveChangesAsync();
        var svc = NewSvc(db);
        await svc.CrearCuentaAsync(EmpresaA, Cuenta(), "tester");

        var r = await svc.GenerarQrAsync(EmpresaA, new GenerarQrCobroRequest { DteDocumentoId = 1 });

        r.IsSuccess.Should().BeTrue();
        r.Value!.Monto.Should().Be(150m); // 200 - 50
        r.Value.Referencia.Should().Be("DTE-01-000999");
    }

    [Fact]
    public async Task GenerarQr_SinCuentaActiva_CuentaNotFound()
    {
        var db = NewDb();
        var svc = NewSvc(db);

        var r = await svc.GenerarQrAsync(EmpresaA, new GenerarQrCobroRequest { Monto = 10m });

        r.ErrorCode.Should().Be("CUENTA_NOT_FOUND");
    }

    [Fact]
    public async Task GenerarQr_SinMontoNiFactura_Validation()
    {
        var db = NewDb();
        var svc = NewSvc(db);
        await svc.CrearCuentaAsync(EmpresaA, Cuenta(), "tester");

        var r = await svc.GenerarQrAsync(EmpresaA, new GenerarQrCobroRequest());

        r.ErrorCode.Should().Be("VALIDATION");
    }

    // ─── Mejora 3: link compartible + PDF de solicitud de cobro ─────────────────

    [Fact]
    public async Task GenerarQr_CuentaConUrlPago_MarcaEsLink()
    {
        var db = NewDb();
        var svc = NewSvc(db);
        await svc.CrearCuentaAsync(EmpresaA, Cuenta(url: "https://pagos.example.com/pay?m={monto}&r={referencia}"), "t");

        var r = await svc.GenerarQrAsync(EmpresaA, new GenerarQrCobroRequest { Monto = 99.5m, Referencia = "REF-9" });

        r.IsSuccess.Should().BeTrue();
        r.Value!.EsLink.Should().BeTrue();
        r.Value.Payload.Should().Be("https://pagos.example.com/pay?m=99.50&r=REF-9");
        r.Value.CuentaCobroId.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GenerarQr_Transferencia_NoEsLink_YExponeDatosDeCuenta()
    {
        var db = NewDb();
        var svc = NewSvc(db);
        await svc.CrearCuentaAsync(EmpresaA, Cuenta(), "t");

        var r = await svc.GenerarQrAsync(EmpresaA, new GenerarQrCobroRequest { Monto = 10m });

        r.Value!.EsLink.Should().BeFalse();
        r.Value.Banco.Should().Be("Banco X");
        r.Value.NumeroCuenta.Should().Be("1234567890");
        r.Value.Titular.Should().Be("Mi Empresa");
    }

    [Fact]
    public async Task GenerarPdf_ProduceDocumentoConNombre()
    {
        var db = NewDb();
        var svc = NewSvc(db);
        await svc.CrearCuentaAsync(EmpresaA, Cuenta(), "t");

        var r = await svc.GenerarPdfAsync(EmpresaA, new GenerarQrCobroRequest { Monto = 75m, Referencia = "FAC-7" });

        r.IsSuccess.Should().BeTrue(r.Error);
        r.Value!.FileName.Should().Be("cobro-FAC-7.pdf");
        Encoding.ASCII.GetString(r.Value.Pdf, 1, 3).Should().Be("PDF");
    }

    [Fact]
    public async Task GenerarPdf_SinCuentaActiva_Falla()
    {
        var svc = NewSvc(NewDb());

        var r = await svc.GenerarPdfAsync(EmpresaA, new GenerarQrCobroRequest { Monto = 10m });

        r.ErrorCode.Should().Be("CUENTA_NOT_FOUND");
    }
}
