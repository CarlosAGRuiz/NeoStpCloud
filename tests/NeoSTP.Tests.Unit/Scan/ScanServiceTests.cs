using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Common;
using NeoSTP.Application.Profit;
using NeoSTP.Application.Profit.Dtos;
using NeoSTP.Application.Scan.Dtos;
using NeoSTP.Domain.Core.Empresas;
using NeoSTP.Domain.Core.Scan;
using NeoSTP.Infrastructure.Persistence;
using NeoSTP.Infrastructure.Scan;
using NeoSTP.Infrastructure.Services;
using NSubstitute;
using Xunit;

namespace NeoSTP.Tests.Unit.Scan;

/// <summary>
/// NeoScanAI: bandeja, subida con extracción mock, corrección y confirmación a
/// gasto/compra/DTE recibido (alimenta NeoProfit), aislamiento por empresa.
/// </summary>
public class ScanServiceTests
{
    private const int EmpresaA = 40;
    private const int EmpresaB = 41;

    private static NeoStpDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<NeoStpDbContext>()
            .UseInMemoryDatabase($"scan-{Guid.NewGuid()}")
            .Options;
        var db = new NeoStpDbContext(options);
        db.Empresas.Add(new Empresa { Id = EmpresaA, Nit = "A", RazonSocial = "A", EstadoCodigo = "ACTIVA" });
        db.Empresas.Add(new Empresa { Id = EmpresaB, Nit = "B", RazonSocial = "B", EstadoCodigo = "ACTIVA" });
        db.SaveChanges();
        return db;
    }

    private static (ScanService svc, IProfitService profit) NewSvc(NeoStpDbContext db)
    {
        var profit = Substitute.For<IProfitService>();
        var svc = new ScanService(db, new MockScanExtractionService(), profit, Substitute.For<IAuditoriaService>());
        return (svc, profit);
    }

    private static SubirScanRequest Captura()
        => new() { Nombre = "factura.jpg", ContentType = "image/jpeg", ContenidoBase64 = Convert.ToBase64String(new byte[] { 1, 2, 3, 4 }) };

    [Fact]
    public async Task Subir_ConMock_QuedaRequiereRevision_YGuardaArchivo()
    {
        var db = NewDb();
        var (svc, _) = NewSvc(db);

        var r = await svc.SubirAsync(EmpresaA, Captura(), "tester");

        r.IsSuccess.Should().BeTrue();
        r.Value!.EstadoCodigo.Should().Be(ScanEstados.RequiereRevision);
        r.Value.TieneArchivo.Should().BeTrue();
        r.Value.Confianza.Should().Be(0m);

        var arch = await svc.GetArchivoAsync(EmpresaA, r.Value.Id);
        arch.Should().NotBeNull();
        arch!.ContentType.Should().Be("image/jpeg");
    }

    [Fact]
    public async Task Subir_Base64Invalido_Validation()
    {
        var db = NewDb();
        var (svc, _) = NewSvc(db);

        var r = await svc.SubirAsync(EmpresaA, new SubirScanRequest { ContenidoBase64 = "no-es-base64-!!" }, "tester");

        r.IsFailure.Should().BeTrue();
        r.ErrorCode.Should().Be("VALIDATION");
    }

    [Fact]
    public async Task ConfirmarComoGasto_CreaProfitGasto_YMarcaConfirmado()
    {
        var db = NewDb();
        var (svc, profit) = NewSvc(db);
        profit.CreateGastoAsync(EmpresaA, Arg.Any<CreateProfitGastoRequest>(), "tester", Arg.Any<CancellationToken>())
            .Returns(Result<ProfitGastoDto>.Ok(new ProfitGastoDto { Id = 77 }));
        var scan = (await svc.SubirAsync(EmpresaA, Captura(), "tester")).Value!;

        var r = await svc.ConfirmarComoGastoAsync(EmpresaA, scan.Id, new CreateProfitGastoRequest { Descripcion = "Combustible", Monto = 25m }, "tester");

        r.IsSuccess.Should().BeTrue();
        r.Value!.EstadoCodigo.Should().Be(ScanEstados.Confirmado);
        r.Value.TipoClasificacion.Should().Be(ScanTipos.Gasto);
        r.Value.ProfitGastoId.Should().Be(77);
        await profit.Received(1).CreateGastoAsync(EmpresaA, Arg.Any<CreateProfitGastoRequest>(), "tester", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ConfirmarComoCompra_CreaProfitCompra()
    {
        var db = NewDb();
        var (svc, profit) = NewSvc(db);
        profit.CreateCompraAsync(EmpresaA, Arg.Any<CreateProfitCompraRequest>(), "tester", Arg.Any<CancellationToken>())
            .Returns(Result<ProfitCompraDto>.Ok(new ProfitCompraDto { Id = 88 }));
        var scan = (await svc.SubirAsync(EmpresaA, Captura(), "tester")).Value!;

        var r = await svc.ConfirmarComoCompraAsync(EmpresaA, scan.Id, new CreateProfitCompraRequest { Proveedor = "Acme", Subtotal = 100m }, "tester");

        r.Value!.ProfitCompraId.Should().Be(88);
        r.Value.TipoClasificacion.Should().Be(ScanTipos.Compra);
    }

    [Fact]
    public async Task RegistrarDteRecibido_CreaRegistroYConfirma()
    {
        var db = NewDb();
        var (svc, _) = NewSvc(db);
        var scan = (await svc.SubirAsync(EmpresaA, Captura(), "tester")).Value!;

        var r = await svc.RegistrarDteRecibidoAsync(EmpresaA, scan.Id, new RegistrarDteRecibidoRequest
        {
            EmisorNombre = "Proveedor X", Total = 150m, Iva = 17m, Subtotal = 133m,
        }, "tester");

        r.IsSuccess.Should().BeTrue();
        r.Value!.DteRecibidoId.Should().NotBeNull();
        (await db.DteDocumentosRecibidos.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Confirmar_YaConfirmado_InvalidState()
    {
        var db = NewDb();
        var (svc, profit) = NewSvc(db);
        profit.CreateGastoAsync(EmpresaA, Arg.Any<CreateProfitGastoRequest>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Result<ProfitGastoDto>.Ok(new ProfitGastoDto { Id = 1 }));
        var scan = (await svc.SubirAsync(EmpresaA, Captura(), "tester")).Value!;
        await svc.ConfirmarComoGastoAsync(EmpresaA, scan.Id, new CreateProfitGastoRequest { Descripcion = "x", Monto = 1m }, "tester");

        var r2 = await svc.ConfirmarComoGastoAsync(EmpresaA, scan.Id, new CreateProfitGastoRequest { Descripcion = "y", Monto = 2m }, "tester");

        r2.ErrorCode.Should().Be("INVALID_STATE");
    }

    [Fact]
    public async Task Rechazar_DejaRechazado()
    {
        var db = NewDb();
        var (svc, _) = NewSvc(db);
        var scan = (await svc.SubirAsync(EmpresaA, Captura(), "tester")).Value!;

        (await svc.RechazarAsync(EmpresaA, scan.Id, "ilegible", "tester")).IsSuccess.Should().BeTrue();
        (await svc.GetAsync(EmpresaA, scan.Id)).Value!.EstadoCodigo.Should().Be(ScanEstados.Rechazado);
    }

    [Fact]
    public async Task Get_DeOtraEmpresa_NotFound()
    {
        var db = NewDb();
        var (svc, _) = NewSvc(db);
        var scan = (await svc.SubirAsync(EmpresaA, Captura(), "tester")).Value!;

        (await svc.GetAsync(EmpresaB, scan.Id)).ErrorCode.Should().Be("SCAN_NOT_FOUND");
    }
}
