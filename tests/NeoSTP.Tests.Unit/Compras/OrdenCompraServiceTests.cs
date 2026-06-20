using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Common;
using NeoSTP.Application.Compras;
using NeoSTP.Application.Compras.Dtos;
using NeoSTP.Domain.Core.Compras;
using NeoSTP.Domain.Core.Empresas;
using NeoSTP.Domain.Core.Productos;
using NeoSTP.Infrastructure.Persistence;
using NeoSTP.Infrastructure.Services;
using NSubstitute;
using Xunit;

namespace NeoSTP.Tests.Unit.Compras;

public class OrdenCompraServiceTests
{
    private const int Empresa = 71;
    private const int OtraEmpresa = 72;

    private static NeoStpDbContext NewDb()
    {
        var db = new NeoStpDbContext(new DbContextOptionsBuilder<NeoStpDbContext>()
            .UseInMemoryDatabase($"orden-compra-{Guid.NewGuid()}").Options);
        db.Empresas.AddRange(
            new Empresa { Id = Empresa, Nit = "E1", RazonSocial = "Empresa 1", EstadoCodigo = "ACTIVA" },
            new Empresa { Id = OtraEmpresa, Nit = "E2", RazonSocial = "Empresa 2", EstadoCodigo = "ACTIVA" });
        db.Proveedores.Add(new Proveedor
        {
            Id = 10, EmpresaId = Empresa, Codigo = "PROV-1", Nombre = "Proveedor Uno",
            EstadoCodigo = ProveedorEstados.Activo,
        });
        db.Productos.AddRange(
            new Producto
            {
                Id = 100, EmpresaId = Empresa, CodigoInterno = "BIEN-1", Nombre = "Insumo",
                TipoItem = "BIEN", UnidadMedidaCodigo = "59", PrecioUnitario = 10m,
                AplicaIva = true, EstadoCodigo = "ACTIVO",
            },
            new Producto
            {
                Id = 101, EmpresaId = Empresa, CodigoInterno = "SERV-1", Nombre = "Servicio",
                TipoItem = "SERVICIO", UnidadMedidaCodigo = "99", PrecioUnitario = 20m,
                AplicaIva = false, EstadoCodigo = "ACTIVO",
            },
            new Producto
            {
                Id = 200, EmpresaId = OtraEmpresa, CodigoInterno = "OTRO", Nombre = "Ajeno",
                TipoItem = "BIEN", UnidadMedidaCodigo = "59", PrecioUnitario = 5m,
                AplicaIva = true, EstadoCodigo = "ACTIVO",
            });
        db.SaveChanges();
        return db;
    }

    private static (OrdenCompraService Service, ICompraService Compras) NewService(NeoStpDbContext db)
    {
        var compras = Substitute.For<ICompraService>();
        compras.CrearFacturaAsync(Arg.Any<int>(), Arg.Any<CrearFacturaCompraRequest>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result<FacturaCompraDetalleDto>.Ok(new FacturaCompraDetalleDto { Id = 900 }));
        return (new OrdenCompraService(db, compras, Substitute.For<IAuditoriaService>()), compras);
    }

    private static GuardarOrdenCompraRequest Request(params GuardarOrdenCompraLineaRequest[] lineas) => new()
    {
        ProveedorId = 10,
        Fecha = new DateOnly(2026, 6, 20),
        FechaEntregaEsperada = new DateOnly(2026, 6, 25),
        Observaciones = "Compra mensual",
        Lineas = lineas.Length == 0
            ? [new GuardarOrdenCompraLineaRequest { ProductoId = 100, Cantidad = 2m, PrecioUnitario = 100m }]
            : lineas.ToList(),
    };

    [Fact]
    public void Calculator_CalculaIvaYTotaliza()
    {
        var gravada = OrdenCompraCalculator.CalcularLinea(2m, 100m, aplicaIva: true);
        var exenta = OrdenCompraCalculator.CalcularLinea(1m, 50m, aplicaIva: false);

        gravada.Should().Be(new OrdenCompraLineaCalculo(200m, 26m, 226m));
        OrdenCompraCalculator.Totalizar([gravada, exenta])
            .Should().Be(new OrdenCompraLineaCalculo(250m, 26m, 276m));
    }

    [Fact]
    public async Task Crear_CalculaMontosYGuardaBorradorTenantSafe()
    {
        var db = NewDb(); var (service, _) = NewService(db);

        var result = await service.CrearAsync(Empresa, Request(), "admin");

        result.IsSuccess.Should().BeTrue();
        result.Value!.EstadoCodigo.Should().Be(OrdenCompraEstados.Borrador);
        result.Value.Subtotal.Should().Be(200m);
        result.Value.Iva.Should().Be(26m);
        result.Value.Total.Should().Be(226m);
        result.Value.Numero.Should().StartWith("OC-");
        db.OrdenesCompra.Should().ContainSingle(x => x.EmpresaId == Empresa);
        (await service.GetAsync(OtraEmpresa, result.Value.Id)).ErrorCode.Should().Be("ORDEN_COMPRA_NOT_FOUND");
    }

    [Fact]
    public async Task Crear_ProductoDeOtraEmpresa_Falla()
    {
        var db = NewDb(); var (service, _) = NewService(db);
        var request = Request(new GuardarOrdenCompraLineaRequest
        { ProductoId = 200, Cantidad = 1m, PrecioUnitario = 5m });

        var result = await service.CrearAsync(Empresa, request, "admin");

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("PRODUCTO_NOT_FOUND");
        db.OrdenesCompra.Should().BeEmpty();
    }

    [Fact]
    public async Task Editar_DespuesDeEmitir_Falla()
    {
        var db = NewDb(); var (service, _) = NewService(db);
        var orden = await service.CrearAsync(Empresa, Request(), "admin");
        await service.EmitirAsync(Empresa, orden.Value!.Id, "admin");

        var result = await service.ActualizarAsync(Empresa, orden.Value.Id, Request(), "admin");

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("INVALID_STATE");
    }

    [Fact]
    public async Task Cancelar_Borrador_BloqueaEmisionPosterior()
    {
        var db = NewDb(); var (service, _) = NewService(db);
        var orden = await service.CrearAsync(Empresa, Request(), "admin");

        var cancelada = await service.CancelarAsync(Empresa, orden.Value!.Id, "admin");
        var emitir = await service.EmitirAsync(Empresa, orden.Value.Id, "admin");

        cancelada.Value!.EstadoCodigo.Should().Be(OrdenCompraEstados.Cancelada);
        emitir.ErrorCode.Should().Be("INVALID_STATE");
    }

    [Fact]
    public async Task ConvertirAFactura_UsaTotalesServerYEnviaSoloBienesAInventario()
    {
        var db = NewDb(); var (service, compras) = NewService(db);
        var orden = await service.CrearAsync(Empresa, Request(
            new GuardarOrdenCompraLineaRequest { ProductoId = 100, Cantidad = 2m, PrecioUnitario = 10m },
            new GuardarOrdenCompraLineaRequest { ProductoId = 101, Cantidad = 1m, PrecioUnitario = 20m }), "admin");
        await service.EmitirAsync(Empresa, orden.Value!.Id, "admin");

        var result = await service.ConvertirAFacturaAsync(Empresa, orden.Value.Id, new ConvertirOrdenCompraRequest
        { NumeroDocumento = "CCF-900", TipoDocumento = "CCF" }, "admin");

        result.IsSuccess.Should().BeTrue();
        result.Value!.EstadoCodigo.Should().Be(OrdenCompraEstados.Recibida);
        result.Value.FacturaCompraId.Should().Be(900);
        await compras.Received(1).CrearFacturaAsync(Empresa,
            Arg.Is<CrearFacturaCompraRequest>(x =>
                x.ProveedorId == 10 && x.Subtotal == 40m && x.Iva == 2.60m && x.TotalEsperado() == 42.60m &&
                x.LineasInventario != null && x.LineasInventario.Count == 1 && x.LineasInventario[0].ProductoId == 100),
            "admin", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ConvertirAFactura_SegundoIntentoNoDuplica()
    {
        var db = NewDb(); var (service, compras) = NewService(db);
        var orden = await service.CrearAsync(Empresa, Request(), "admin");
        await service.EmitirAsync(Empresa, orden.Value!.Id, "admin");
        var request = new ConvertirOrdenCompraRequest { NumeroDocumento = "F-1" };
        await service.ConvertirAFacturaAsync(Empresa, orden.Value.Id, request, "admin");

        var second = await service.ConvertirAFacturaAsync(Empresa, orden.Value.Id, request, "admin");

        second.ErrorCode.Should().Be("INVALID_STATE");
        await compras.Received(1).CrearFacturaAsync(Empresa, Arg.Any<CrearFacturaCompraRequest>(), "admin", Arg.Any<CancellationToken>());
    }
}

internal static class OrdenCompraTestExtensions
{
    public static decimal TotalEsperado(this CrearFacturaCompraRequest request) => request.Subtotal + request.Iva;
}
