using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Common;
using NeoSTP.Application.Inventario.Dtos;
using NeoSTP.Domain.Core.Empresas;
using NeoSTP.Domain.Core.Inventario;
using NeoSTP.Domain.Core.Productos;
using NeoSTP.Infrastructure.Persistence;
using NeoSTP.Infrastructure.Services;
using NSubstitute;
using Xunit;

namespace NeoSTP.Tests.Unit.Inventario;

/// <summary>E2 — inventario por sucursal: saldos independientes, consolidado, traslados y FEFO por sucursal.</summary>
public class InventarioSucursalTests
{
    private const int Empresa = 63;
    private const int SucNorte = 1;
    private const int SucSur = 2;

    private static NeoStpDbContext NewDb(bool controlaLote = false)
    {
        var options = new DbContextOptionsBuilder<NeoStpDbContext>()
            .UseInMemoryDatabase($"invsuc-{Guid.NewGuid()}").Options;
        var db = new NeoStpDbContext(options);
        db.Empresas.Add(new Empresa { Id = Empresa, Nit = "X", RazonSocial = "Cadena", EstadoCodigo = "ACTIVA" });
        db.Sucursales.Add(new Sucursal { Id = SucNorte, EmpresaId = Empresa, Codigo = "NORTE", Nombre = "Norte", EstadoCodigo = "ACTIVO" });
        db.Sucursales.Add(new Sucursal { Id = SucSur, EmpresaId = Empresa, Codigo = "SUR", Nombre = "Sur", EstadoCodigo = "ACTIVO" });
        db.Productos.Add(new Producto
        {
            Id = 1, EmpresaId = Empresa, CodigoInterno = "P1", Nombre = "Producto", PrecioUnitario = 10m,
            CostoUnitario = 2m, ControlaLote = controlaLote, EstadoCodigo = "ACTIVO",
            UnidadMedidaCodigo = "59", TipoItem = "BIEN",
        });
        db.SaveChanges();
        return db;
    }

    private static InventarioService NewSvc(NeoStpDbContext db)
        => new(db, Substitute.For<IAuditoriaService>());

    private static RegistrarMovimientoInventarioRequest Mov(decimal cant, int? suc = null, string? lote = null, DateOnly? vence = null) => new()
    { ProductoId = 1, Cantidad = cant, CostoUnitario = 2m, Origen = "COMPRA", SucursalId = suc, NumeroLote = lote, FechaVencimiento = vence };

    [Fact]
    public async Task Entradas_PorSucursal_MantienenSaldosIndependientes()
    {
        var db = NewDb(); var svc = NewSvc(db);

        await svc.RegistrarEntradaAsync(Empresa, Mov(10, SucNorte), "t");
        await svc.RegistrarEntradaAsync(Empresa, Mov(5, SucSur), "t");
        await svc.RegistrarEntradaAsync(Empresa, Mov(3), "t"); // central

        (await db.ExistenciasProducto.CountAsync()).Should().Be(3);
        (await svc.GetExistenciaAsync(Empresa, 1, SucNorte)).Value!.Cantidad.Should().Be(10m);
        (await svc.GetExistenciaAsync(Empresa, 1, SucSur)).Value!.Cantidad.Should().Be(5m);
        (await svc.GetExistenciaAsync(Empresa, 1)).Value!.Cantidad.Should().Be(18m); // consolidado
    }

    [Fact]
    public async Task Salida_SoloDescuentaSuSucursal_YValidaSuStock()
    {
        var db = NewDb(); var svc = NewSvc(db);
        await svc.RegistrarEntradaAsync(Empresa, Mov(10, SucNorte), "t");
        await svc.RegistrarEntradaAsync(Empresa, Mov(5, SucSur), "t");

        (await svc.RegistrarSalidaAsync(Empresa, Mov(4, SucNorte), "t")).IsSuccess.Should().BeTrue();
        (await svc.GetExistenciaAsync(Empresa, 1, SucNorte)).Value!.Cantidad.Should().Be(6m);
        (await svc.GetExistenciaAsync(Empresa, 1, SucSur)).Value!.Cantidad.Should().Be(5m);

        // Sur solo tiene 5 aunque el consolidado tenga 11.
        (await svc.RegistrarSalidaAsync(Empresa, Mov(8, SucSur), "t")).ErrorCode.Should().Be("STOCK_INSUFICIENTE");
    }

    [Fact]
    public async Task Sucursal_Inexistente_Falla()
    {
        var svc = NewSvc(NewDb());
        (await svc.RegistrarEntradaAsync(Empresa, Mov(1, 999), "t")).ErrorCode.Should().Be("SUCURSAL_NOT_FOUND");
    }

    [Fact]
    public async Task Traslado_MueveSaldo_YDejaKardexEnAmbosLados()
    {
        var db = NewDb(); var svc = NewSvc(db);
        await svc.RegistrarEntradaAsync(Empresa, Mov(10, SucNorte), "t");

        var r = await svc.TrasladarAsync(Empresa, new TrasladoInventarioRequest
        { ProductoId = 1, Cantidad = 4, SucursalOrigenId = SucNorte, SucursalDestinoId = SucSur }, "t");

        r.IsSuccess.Should().BeTrue(r.Error);
        (await svc.GetExistenciaAsync(Empresa, 1, SucNorte)).Value!.Cantidad.Should().Be(6m);
        (await svc.GetExistenciaAsync(Empresa, 1, SucSur)).Value!.Cantidad.Should().Be(4m);
        (await svc.GetExistenciaAsync(Empresa, 1)).Value!.Cantidad.Should().Be(10m); // consolidado no cambia

        var traslados = await db.MovimientosInventario
            .Where(m => m.Origen == OrigenesMovimientoInventario.Traslado).ToListAsync();
        traslados.Should().HaveCount(2);
        traslados.Select(m => m.Referencia).Distinct().Should().HaveCount(1); // misma referencia
        traslados.Single(m => m.Tipo == TiposMovimientoInventario.Salida).SucursalId.Should().Be(SucNorte);
        traslados.Single(m => m.Tipo == TiposMovimientoInventario.Entrada).SucursalId.Should().Be(SucSur);
    }

    [Fact]
    public async Task Traslado_Invalido_MismoDestino_OSinStock()
    {
        var db = NewDb(); var svc = NewSvc(db);
        await svc.RegistrarEntradaAsync(Empresa, Mov(2, SucNorte), "t");

        (await svc.TrasladarAsync(Empresa, new TrasladoInventarioRequest
        { ProductoId = 1, Cantidad = 1, SucursalOrigenId = SucNorte, SucursalDestinoId = SucNorte }, "t"))
            .ErrorCode.Should().Be("VALIDATION");

        (await svc.TrasladarAsync(Empresa, new TrasladoInventarioRequest
        { ProductoId = 1, Cantidad = 5, SucursalOrigenId = SucNorte, SucursalDestinoId = SucSur }, "t"))
            .ErrorCode.Should().Be("STOCK_INSUFICIENTE");
    }

    [Fact]
    public async Task Traslado_ConLotes_ReplicaLoteYVencimientoEnDestino()
    {
        var db = NewDb(controlaLote: true); var svc = NewSvc(db);
        var vence = new DateOnly(2027, 3, 1);
        await svc.RegistrarEntradaAsync(Empresa, Mov(10, SucNorte, "L1", vence), "t");

        var r = await svc.TrasladarAsync(Empresa, new TrasladoInventarioRequest
        { ProductoId = 1, Cantidad = 4, SucursalOrigenId = SucNorte, SucursalDestinoId = SucSur }, "t");

        r.IsSuccess.Should().BeTrue(r.Error);
        var lotes = await db.LotesProducto.OrderBy(l => l.SucursalId).ToListAsync();
        lotes.Should().HaveCount(2);
        lotes.Single(l => l.SucursalId == SucNorte).Cantidad.Should().Be(6m);
        var destino = lotes.Single(l => l.SucursalId == SucSur);
        destino.Cantidad.Should().Be(4m);
        destino.NumeroLote.Should().Be("L1");
        destino.FechaVencimiento.Should().Be(vence);
    }

    [Fact]
    public async Task Fefo_EsPorSucursal_NoCruzaLotesDeOtraSucursal()
    {
        var db = NewDb(controlaLote: true); var svc = NewSvc(db);
        await svc.RegistrarEntradaAsync(Empresa, Mov(5, SucNorte, "LN"), "t");
        await svc.RegistrarEntradaAsync(Empresa, Mov(5, SucSur, "LS"), "t");

        await svc.RegistrarSalidaAsync(Empresa, Mov(3, SucNorte), "t");

        (await db.LotesProducto.SingleAsync(l => l.NumeroLote == "LN")).Cantidad.Should().Be(2m);
        (await db.LotesProducto.SingleAsync(l => l.NumeroLote == "LS")).Cantidad.Should().Be(5m);
    }

    [Fact]
    public async Task ListExistencias_ConsolidadoYPorSucursal()
    {
        var db = NewDb(); var svc = NewSvc(db);
        await svc.RegistrarEntradaAsync(Empresa, Mov(10, SucNorte), "t");
        await svc.RegistrarEntradaAsync(Empresa, Mov(5, SucSur), "t");

        var consolidado = await svc.ListExistenciasAsync(Empresa, false, new PagedQuery());
        var norte = await svc.ListExistenciasAsync(Empresa, false, new PagedQuery(), SucNorte);

        consolidado.Value!.Items.Single().Cantidad.Should().Be(15m);
        norte.Value!.Items.Single().Cantidad.Should().Be(10m);
    }
}
