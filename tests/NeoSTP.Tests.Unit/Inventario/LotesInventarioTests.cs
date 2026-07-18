using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Cobranza;
using NeoSTP.Application.Cobranza.Dtos;
using NeoSTP.Application.Common;
using NeoSTP.Application.Inventario.Dtos;
using NeoSTP.Application.Notificaciones;
using NeoSTP.Application.Notificaciones.Dtos;
using NeoSTP.Domain.Core.Empresas;
using NeoSTP.Domain.Core.Inventario;
using NeoSTP.Domain.Core.Notificaciones;
using NeoSTP.Domain.Core.Productos;
using NeoSTP.Infrastructure.Persistence;
using NeoSTP.Infrastructure.Services;
using NSubstitute;
using Xunit;

namespace NeoSTP.Tests.Unit.Inventario;

/// <summary>Entrega 3 — lotes y vencimientos: entradas con lote, consumo FEFO y alertas.</summary>
public class LotesInventarioTests
{
    private const int Empresa = 62;

    private static NeoStpDbContext NewDb(bool controlaLote = true)
    {
        var options = new DbContextOptionsBuilder<NeoStpDbContext>()
            .UseInMemoryDatabase($"lote-{Guid.NewGuid()}").Options;
        var db = new NeoStpDbContext(options);
        db.Empresas.Add(new Empresa { Id = Empresa, Nit = "X", RazonSocial = "Farmacia", EstadoCodigo = "ACTIVA" });
        db.Productos.Add(new Producto
        {
            Id = 1, EmpresaId = Empresa, CodigoInterno = "MED1", Nombre = "Ibuprofeno", PrecioUnitario = 1m,
            CostoUnitario = 0.5m, ControlaLote = controlaLote, EstadoCodigo = "ACTIVO",
            UnidadMedidaCodigo = "59", TipoItem = "BIEN",
        });
        db.SaveChanges();
        return db;
    }

    private static InventarioService NewSvc(NeoStpDbContext db)
        => new(db, Substitute.For<IAuditoriaService>());

    private static RegistrarMovimientoInventarioRequest Mov(decimal cant, string? lote = null, DateOnly? vence = null) => new()
    { ProductoId = 1, Cantidad = cant, CostoUnitario = 0.5m, Origen = "COMPRA", NumeroLote = lote, FechaVencimiento = vence };

    [Fact]
    public async Task Entrada_ConLote_CreaYAcumulaLote()
    {
        var db = NewDb(); var svc = NewSvc(db);

        (await svc.RegistrarEntradaAsync(Empresa, Mov(10, "L1", new DateOnly(2026, 12, 31)), "t")).IsSuccess.Should().BeTrue();
        (await svc.RegistrarEntradaAsync(Empresa, Mov(5, "l1"), "t")).IsSuccess.Should().BeTrue(); // case-insensitive

        var lote = await db.LotesProducto.SingleAsync();
        lote.NumeroLote.Should().Be("L1");
        lote.Cantidad.Should().Be(15m);
        lote.FechaVencimiento.Should().Be(new DateOnly(2026, 12, 31));
        (await db.MovimientosInventario.OrderBy(m => m.Id).FirstAsync()).NumeroLote.Should().Be("L1");
    }

    [Fact]
    public async Task Entrada_SinLote_EnProductoControlado_Falla()
    {
        var svc = NewSvc(NewDb());

        var r = await svc.RegistrarEntradaAsync(Empresa, Mov(10), "t");

        r.ErrorCode.Should().Be("LOTE_REQUERIDO");
    }

    [Fact]
    public async Task Salida_Fefo_ConsumePrimeroLoQueVencePrimero()
    {
        var db = NewDb(); var svc = NewSvc(db);
        await svc.RegistrarEntradaAsync(Empresa, Mov(10, "TARDIO", new DateOnly(2027, 6, 1)), "t");
        await svc.RegistrarEntradaAsync(Empresa, Mov(10, "PRONTO", new DateOnly(2026, 9, 1)), "t");
        await svc.RegistrarEntradaAsync(Empresa, Mov(10, "SINVENC"), "t");

        var r = await svc.RegistrarSalidaAsync(Empresa, Mov(15), "t");

        r.IsSuccess.Should().BeTrue(r.Error);
        var lotes = await db.LotesProducto.ToDictionaryAsync(l => l.NumeroLote, l => l.Cantidad);
        lotes["PRONTO"].Should().Be(0m);   // venció primero → se agotó
        lotes["TARDIO"].Should().Be(5m);   // completó la salida
        lotes["SINVENC"].Should().Be(10m); // sin vencimiento va al final
        var mov = await db.MovimientosInventario.OrderByDescending(m => m.Id).FirstAsync();
        mov.NumeroLote.Should().Be("FEFO");
        mov.Nota.Should().Contain("PRONTO").And.Contain("TARDIO");
    }

    [Fact]
    public async Task Salida_DeLoteEspecifico_DescuentaEseLote_YValidaSaldo()
    {
        var db = NewDb(); var svc = NewSvc(db);
        await svc.RegistrarEntradaAsync(Empresa, Mov(10, "A"), "t");
        await svc.RegistrarEntradaAsync(Empresa, Mov(10, "B"), "t");

        (await svc.RegistrarSalidaAsync(Empresa, Mov(4, "B"), "t")).IsSuccess.Should().BeTrue();
        (await db.LotesProducto.SingleAsync(l => l.NumeroLote == "B")).Cantidad.Should().Be(6m);

        (await svc.RegistrarSalidaAsync(Empresa, Mov(9, "B"), "t")).ErrorCode.Should().Be("LOTE_INSUFICIENTE");
        (await svc.RegistrarSalidaAsync(Empresa, Mov(1, "ZZZ"), "t")).ErrorCode.Should().Be("LOTE_NOT_FOUND");
    }

    [Fact]
    public async Task Salida_StockPrevioSinLotes_NoBloquea()
    {
        // Producto que activó control de lote con stock existente (sin lotes registrados).
        var db = NewDb(controlaLote: false); var svc = NewSvc(db);
        await svc.RegistrarEntradaAsync(Empresa, Mov(20), "t"); // entrada sin lote (aún no controlaba)
        var prod = await db.Productos.FirstAsync();
        prod.ControlaLote = true;
        await db.SaveChangesAsync();

        var r = await svc.RegistrarSalidaAsync(Empresa, Mov(5), "t");

        r.IsSuccess.Should().BeTrue(r.Error);
        (await db.MovimientosInventario.OrderByDescending(m => m.Id).FirstAsync()).NumeroLote.Should().Be("SIN_LOTE");
    }

    [Fact]
    public async Task ListLotes_MarcaVencidosYPorVencer()
    {
        var db = NewDb(); var svc = NewSvc(db);
        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
        await svc.RegistrarEntradaAsync(Empresa, Mov(5, "VENCIDO", hoy.AddDays(-1)), "t");
        await svc.RegistrarEntradaAsync(Empresa, Mov(5, "CERCA", hoy.AddDays(10)), "t");
        await svc.RegistrarEntradaAsync(Empresa, Mov(5, "LEJOS", hoy.AddDays(200)), "t");

        var todos = await svc.ListLotesAsync(Empresa);
        var criticos = await svc.ListLotesAsync(Empresa, soloPorVencer: true, diasUmbral: 30);

        todos.Value!.Count.Should().Be(3);
        criticos.Value!.Select(l => l.NumeroLote).Should().BeEquivalentTo(new[] { "VENCIDO", "CERCA" });
        criticos.Value.Single(l => l.NumeroLote == "VENCIDO").Vencido.Should().BeTrue();
    }

    [Fact]
    public async Task AlertaGeneracion_CreaAlertaDeLotePorVencer()
    {
        var db = NewDb(); var inv = NewSvc(db);
        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
        await inv.RegistrarEntradaAsync(Empresa, Mov(5, "CADUCA", hoy.AddDays(7)), "t");
        await inv.RegistrarEntradaAsync(Empresa, Mov(5, "OK", hoy.AddDays(300)), "t");

        var alertas = Substitute.For<IAlertaService>();
        var cobranza = Substitute.For<ICobranzaService>();
        cobranza.GetPendientesAsync(Arg.Any<int>(), Arg.Any<CobranzaQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<PagedResult<CobroPendienteDto>>.Ok(PagedResult<CobroPendienteDto>.Create(new List<CobroPendienteDto>(), 0, 1, 50)));
        var gen = new AlertaGeneracionService(db, alertas, cobranza);

        var creadas = await gen.GenerarAsync(Empresa);

        creadas.Should().BeGreaterThan(0);
        await alertas.Received(1).CrearAsync(
            Arg.Is<CrearAlertaRequest>(r => r.TipoCodigo == AlertaTipos.LotePorVencer && r.Mensaje.Contains("CADUCA")),
            Arg.Any<CancellationToken>());
    }
}
