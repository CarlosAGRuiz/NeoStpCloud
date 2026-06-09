using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Inventario.Dtos;
using NeoSTP.Domain.Core.Empresas;
using NeoSTP.Domain.Core.Productos;
using NeoSTP.Infrastructure.Persistence;
using NeoSTP.Infrastructure.Services;
using NSubstitute;
using Xunit;

namespace NeoSTP.Tests.Unit.Inventario;

/// <summary>INVENTARIO — InventarioService: entradas/salidas/ajustes, costo y stock.</summary>
public class InventarioServiceTests
{
    private const int Empresa = 61;

    private static NeoStpDbContext NewDb(decimal? costoProducto = 0m)
    {
        var options = new DbContextOptionsBuilder<NeoStpDbContext>()
            .UseInMemoryDatabase($"inv-{Guid.NewGuid()}").Options;
        var db = new NeoStpDbContext(options);
        db.Empresas.Add(new Empresa { Id = Empresa, Nit = "X", RazonSocial = "Tienda", EstadoCodigo = "ACTIVA" });
        db.Productos.Add(new Producto
        {
            Id = 1, EmpresaId = Empresa, CodigoInterno = "P1", Nombre = "Producto", PrecioUnitario = 10m,
            CostoUnitario = costoProducto, AplicaIva = true, EstadoCodigo = "ACTIVO", UnidadMedidaCodigo = "59", TipoItem = "BIEN",
        });
        db.SaveChanges();
        return db;
    }

    private static InventarioService NewSvc(NeoStpDbContext db)
        => new(db, Substitute.For<IAuditoriaService>());

    private static RegistrarMovimientoInventarioRequest Mov(decimal cant, decimal? costo = null) => new()
    { ProductoId = 1, Cantidad = cant, CostoUnitario = costo, Origen = "COMPRA" };

    [Fact]
    public async Task Entrada_CreaExistenciaYActualizaCostoProducto()
    {
        var db = NewDb(); var svc = NewSvc(db);

        var r = await svc.RegistrarEntradaAsync(Empresa, Mov(10, 2m), "t");

        r.IsSuccess.Should().BeTrue();
        r.Value!.Cantidad.Should().Be(10);
        r.Value.CostoPromedio.Should().Be(2m);
        // El costo del producto se actualiza para NeoProfit.
        (await db.Productos.FirstAsync()).CostoUnitario.Should().Be(2m);
    }

    [Fact]
    public async Task Entrada_DosVeces_PromediaPonderado()
    {
        var db = NewDb(); var svc = NewSvc(db);
        await svc.RegistrarEntradaAsync(Empresa, Mov(10, 2m), "t");

        var r = await svc.RegistrarEntradaAsync(Empresa, Mov(10, 4m), "t");

        r.Value!.Cantidad.Should().Be(20);
        r.Value.CostoPromedio.Should().Be(3m);
    }

    [Fact]
    public async Task Salida_ReduceStock_ConservaCosto()
    {
        var db = NewDb(); var svc = NewSvc(db);
        await svc.RegistrarEntradaAsync(Empresa, Mov(10, 3m), "t");

        var r = await svc.RegistrarSalidaAsync(Empresa, new RegistrarMovimientoInventarioRequest { ProductoId = 1, Cantidad = 4, Origen = "VENTA" }, "t");

        r.IsSuccess.Should().BeTrue();
        r.Value!.Cantidad.Should().Be(6);
        r.Value.CostoPromedio.Should().Be(3m);
    }

    [Fact]
    public async Task Salida_Insuficiente_Falla()
    {
        var db = NewDb(); var svc = NewSvc(db);
        await svc.RegistrarEntradaAsync(Empresa, Mov(5, 3m), "t");

        var r = await svc.RegistrarSalidaAsync(Empresa, new RegistrarMovimientoInventarioRequest { ProductoId = 1, Cantidad = 10 }, "t");

        r.IsFailure.Should().BeTrue();
        r.ErrorCode.Should().Be("STOCK_INSUFICIENTE");
    }

    [Fact]
    public async Task Ajuste_FijaCantidadAbsoluta()
    {
        var db = NewDb(); var svc = NewSvc(db);
        await svc.RegistrarEntradaAsync(Empresa, Mov(10, 3m), "t");

        var r = await svc.AjustarAsync(Empresa, new AjusteStockRequest { ProductoId = 1, CantidadAbsoluta = 7 }, "t");

        r.Value!.Cantidad.Should().Be(7);
        (await db.MovimientosInventario.CountAsync()).Should().Be(2); // entrada + ajuste en kardex
    }

    [Fact]
    public async Task Resumen_SumaValorYCuentaStock()
    {
        var db = NewDb(); var svc = NewSvc(db);
        await svc.RegistrarEntradaAsync(Empresa, Mov(10, 2m), "t"); // valor 20
        await svc.SetStockMinimoAsync(Empresa, new SetStockMinimoRequest { ProductoId = 1, StockMinimo = 15 }, "t"); // 10 <= 15 → bajo

        var r = await svc.ResumenAsync(Empresa);

        r.Value!.ValorTotal.Should().Be(20m);
        r.Value.Productos.Should().Be(1);
        r.Value.ProductosBajoStock.Should().Be(1);
    }

    [Fact]
    public async Task Entrada_ProductoInexistente_Falla()
    {
        var db = NewDb(); var svc = NewSvc(db);

        var r = await svc.RegistrarEntradaAsync(Empresa, new RegistrarMovimientoInventarioRequest { ProductoId = 999, Cantidad = 1, CostoUnitario = 1 }, "t");

        r.IsFailure.Should().BeTrue();
        r.ErrorCode.Should().Be("PRODUCTO_NOT_FOUND");
    }
}
