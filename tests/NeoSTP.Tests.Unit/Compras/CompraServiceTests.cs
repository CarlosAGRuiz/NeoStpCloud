using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Common;
using NeoSTP.Application.Compras.Dtos;
using NeoSTP.Application.Profit;
using NeoSTP.Application.Profit.Dtos;
using NeoSTP.Application.Tesoreria;
using NeoSTP.Application.Tesoreria.Dtos;
using NeoSTP.Domain.Core.Empresas;
using NeoSTP.Infrastructure.Persistence;
using NeoSTP.Infrastructure.Services;
using NSubstitute;
using Xunit;

namespace NeoSTP.Tests.Unit.Compras;

/// <summary>NEOCOMPRAS — CompraService: proveedores, facturas (CxP), pagos y saldos.</summary>
public class CompraServiceTests
{
    private const int Empresa = 55;

    private static NeoStpDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<NeoStpDbContext>()
            .UseInMemoryDatabase($"compras-{Guid.NewGuid()}").Options;
        var db = new NeoStpDbContext(options);
        db.Empresas.Add(new Empresa { Id = Empresa, Nit = "X", RazonSocial = "Mi Empresa", EstadoCodigo = "ACTIVA" });
        db.SaveChanges();
        return db;
    }

    private static (CompraService svc, IProfitService profit, ITesoreriaService tes) NewSvc(NeoStpDbContext db)
    {
        var profit = Substitute.For<IProfitService>();
        profit.CreateGastoAsync(Arg.Any<int>(), Arg.Any<CreateProfitGastoRequest>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result<ProfitGastoDto>.Ok(new ProfitGastoDto { Id = 999 }));
        profit.InactivarGastoAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok());
        var tes = Substitute.For<ITesoreriaService>();
        tes.RegistrarMovimientoAsync(Arg.Any<int>(), Arg.Any<RegistrarMovimientoRequest>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result<MovimientoTesoreriaDto>.Ok(new MovimientoTesoreriaDto { Id = 321 }));
        tes.AnularMovimientoAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok());
        var svc = new CompraService(db, Substitute.For<IAuditoriaService>(), profit, tes);
        return (svc, profit, tes);
    }

    private static async Task<int> NuevoProveedor(CompraService svc, int plazo = 30)
    {
        var r = await svc.CrearProveedorAsync(Empresa, new CreateProveedorRequest
        { Codigo = $"P{Guid.NewGuid():N}".Substring(0, 6), Nombre = "Proveedor SA", PlazoDiasDefault = plazo }, "t");
        r.IsSuccess.Should().BeTrue();
        return r.Value!.Id;
    }

    private static CrearFacturaCompraRequest Factura(int provId, decimal sub = 100m, decimal iva = 13m) => new()
    {
        ProveedorId = provId, NumeroDocumento = "F-001", TipoDocumento = "CCF", CondicionPago = "CREDITO",
        FechaEmision = new DateOnly(2026, 6, 1), Subtotal = sub, Iva = iva, IvaDeducible = true,
    };

    [Fact]
    public async Task CrearProveedor_CodigoDuplicado_Falla()
    {
        var db = NewDb(); var (svc, _, _) = NewSvc(db);
        await svc.CrearProveedorAsync(Empresa, new CreateProveedorRequest { Codigo = "ACME", Nombre = "A" }, "t");

        var r = await svc.CrearProveedorAsync(Empresa, new CreateProveedorRequest { Codigo = "ACME", Nombre = "B" }, "t");

        r.IsFailure.Should().BeTrue();
        r.ErrorCode.Should().Be("DUPLICATE");
    }

    [Fact]
    public async Task CrearFactura_CalculaTotalYVencimientoYGasto()
    {
        var db = NewDb(); var (svc, profit, _) = NewSvc(db);
        var pid = await NuevoProveedor(svc, plazo: 30);

        var r = await svc.CrearFacturaAsync(Empresa, Factura(pid, 100m, 13m), "t");

        r.IsSuccess.Should().BeTrue();
        r.Value!.Total.Should().Be(113m);
        r.Value.Saldo.Should().Be(113m);
        r.Value.EstadoCodigo.Should().Be("PENDIENTE");
        r.Value.FechaVencimiento.Should().Be(new DateOnly(2026, 7, 1)); // emisión + 30
        r.Value.ProfitGastoId.Should().Be(999);
        await profit.Received(1).CreateGastoAsync(Empresa, Arg.Is<CreateProfitGastoRequest>(g => g.Categoria == "COMPRA"), "t", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CrearFactura_NumeroDuplicadoMismoProveedor_Falla()
    {
        var db = NewDb(); var (svc, _, _) = NewSvc(db);
        var pid = await NuevoProveedor(svc);
        await svc.CrearFacturaAsync(Empresa, Factura(pid), "t");

        var r = await svc.CrearFacturaAsync(Empresa, Factura(pid), "t");

        r.IsFailure.Should().BeTrue();
        r.ErrorCode.Should().Be("DUPLICATE");
    }

    [Fact]
    public async Task RegistrarPago_Parcial_DejaParcialYReduceSaldo()
    {
        var db = NewDb(); var (svc, _, _) = NewSvc(db);
        var pid = await NuevoProveedor(svc);
        var f = await svc.CrearFacturaAsync(Empresa, Factura(pid, 100m, 13m), "t");

        var r = await svc.RegistrarPagoAsync(Empresa, new RegistrarPagoProveedorRequest
        { FacturaCompraId = f.Value!.Id, Monto = 50m, FormaPagoCodigo = "TRANSFERENCIA" }, "t");

        r.IsSuccess.Should().BeTrue();
        var det = await svc.GetFacturaAsync(Empresa, f.Value.Id);
        det.Value!.Pagado.Should().Be(50m);
        det.Value.Saldo.Should().Be(63m);
        det.Value.EstadoCodigo.Should().Be("PARCIAL");
    }

    [Fact]
    public async Task RegistrarPago_Total_DejaPagada()
    {
        var db = NewDb(); var (svc, _, _) = NewSvc(db);
        var pid = await NuevoProveedor(svc);
        var f = await svc.CrearFacturaAsync(Empresa, Factura(pid, 100m, 13m), "t");

        await svc.RegistrarPagoAsync(Empresa, new RegistrarPagoProveedorRequest { FacturaCompraId = f.Value!.Id, Monto = 113m }, "t");

        (await svc.GetFacturaAsync(Empresa, f.Value.Id)).Value!.EstadoCodigo.Should().Be("PAGADA");
    }

    [Fact]
    public async Task RegistrarPago_ExcedeSaldo_Falla()
    {
        var db = NewDb(); var (svc, _, _) = NewSvc(db);
        var pid = await NuevoProveedor(svc);
        var f = await svc.CrearFacturaAsync(Empresa, Factura(pid, 100m, 13m), "t");

        var r = await svc.RegistrarPagoAsync(Empresa, new RegistrarPagoProveedorRequest { FacturaCompraId = f.Value!.Id, Monto = 200m }, "t");

        r.IsFailure.Should().BeTrue();
        r.ErrorCode.Should().Be("VALIDATION");
    }

    [Fact]
    public async Task RegistrarPago_ConCuentaTesoreria_GeneraEgreso()
    {
        var db = NewDb(); var (svc, _, tes) = NewSvc(db);
        var pid = await NuevoProveedor(svc);
        var f = await svc.CrearFacturaAsync(Empresa, Factura(pid, 100m, 13m), "t");

        var r = await svc.RegistrarPagoAsync(Empresa, new RegistrarPagoProveedorRequest
        { FacturaCompraId = f.Value!.Id, Monto = 113m, CuentaTesoreriaId = 7 }, "t");

        r.IsSuccess.Should().BeTrue();
        r.Value!.MovimientoTesoreriaId.Should().Be(321);
        await tes.Received(1).RegistrarMovimientoAsync(Empresa,
            Arg.Is<RegistrarMovimientoRequest>(m => m.Tipo == "EGRESO" && m.Origen == "COMPRA" && m.CuentaId == 7), "t", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AnularPago_RevierteSaldoYEgreso()
    {
        var db = NewDb(); var (svc, _, tes) = NewSvc(db);
        var pid = await NuevoProveedor(svc);
        var f = await svc.CrearFacturaAsync(Empresa, Factura(pid, 100m, 13m), "t");
        var pago = await svc.RegistrarPagoAsync(Empresa, new RegistrarPagoProveedorRequest
        { FacturaCompraId = f.Value!.Id, Monto = 113m, CuentaTesoreriaId = 7 }, "t");

        var r = await svc.AnularPagoAsync(Empresa, pago.Value!.Id, "t");

        r.IsSuccess.Should().BeTrue();
        await tes.Received(1).AnularMovimientoAsync(Empresa, 321, "t", Arg.Any<CancellationToken>());
        var det = await svc.GetFacturaAsync(Empresa, f.Value.Id);
        det.Value!.Saldo.Should().Be(113m);
        det.Value.EstadoCodigo.Should().Be("PENDIENTE");
    }

    [Fact]
    public async Task AnularFactura_ConPagoConfirmado_Falla()
    {
        var db = NewDb(); var (svc, _, _) = NewSvc(db);
        var pid = await NuevoProveedor(svc);
        var f = await svc.CrearFacturaAsync(Empresa, Factura(pid, 100m, 13m), "t");
        await svc.RegistrarPagoAsync(Empresa, new RegistrarPagoProveedorRequest { FacturaCompraId = f.Value!.Id, Monto = 10m }, "t");

        var r = await svc.AnularFacturaAsync(Empresa, f.Value.Id, "t");

        r.IsFailure.Should().BeTrue();
        r.ErrorCode.Should().Be("INVALID_STATE");
    }

    [Fact]
    public async Task Resumen_SumaSaldosYVencido()
    {
        var db = NewDb(); var (svc, _, _) = NewSvc(db);
        var pid = await NuevoProveedor(svc, plazo: 0);
        // Factura vencida (emisión 2020, contado → vence mismo día)
        await svc.CrearFacturaAsync(Empresa, new CrearFacturaCompraRequest
        { ProveedorId = pid, NumeroDocumento = "V-1", TipoDocumento = "FACTURA", CondicionPago = "CONTADO",
          FechaEmision = new DateOnly(2020, 1, 1), Subtotal = 200m, Iva = 0m }, "t");

        var r = await svc.ResumenAsync(Empresa);

        r.IsSuccess.Should().BeTrue();
        r.Value!.TotalPorPagar.Should().Be(200m);
        r.Value.TotalVencido.Should().Be(200m);
        r.Value.FacturasPendientes.Should().Be(1);
        r.Value.Proveedores.Should().Be(1);
    }
}
