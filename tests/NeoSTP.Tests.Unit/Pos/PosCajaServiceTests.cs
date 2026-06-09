using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Comunicaciones;
using NeoSTP.Application.Connect;
using NeoSTP.Application.Dte.Abstractions;
using NeoSTP.Application.Pos;
using NeoSTP.Application.Pos.Dtos;
using NeoSTP.Domain.Core.Empresas;
using NeoSTP.Domain.Core.Productos;
using NeoSTP.Infrastructure.Persistence;
using NeoSTP.Infrastructure.Pos;
using NeoSTP.Infrastructure.Services;
using NSubstitute;
using Xunit;

namespace NeoSTP.Tests.Unit.Pos;

/// <summary>NEOPOS — PosCajaService: apertura, cierre (corte) e integración venta→caja.</summary>
public class PosCajaServiceTests
{
    private const int Empresa = 88;

    private static NeoStpDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<NeoStpDbContext>()
            .UseInMemoryDatabase($"caja-{Guid.NewGuid()}").Options;
        var db = new NeoStpDbContext(options);
        db.Empresas.Add(new Empresa { Id = Empresa, Nit = "X", RazonSocial = "Tienda", EstadoCodigo = "ACTIVA" });
        db.Productos.Add(new Producto
        {
            Id = 1, EmpresaId = Empresa, CodigoInterno = "P1", Nombre = "Café", PrecioUnitario = 11.30m,
            AplicaIva = true, EstadoCodigo = "ACTIVO", UnidadMedidaCodigo = "59", TipoItem = "BIEN",
        });
        db.SaveChanges();
        return db;
    }

    private static PosCajaService NewCaja(NeoStpDbContext db) => new(db, Substitute.For<IAuditoriaService>());

    private static PosService NewPos(NeoStpDbContext db)
    {
        var email = Substitute.For<ITenantEmailSender>();
        var dte = Substitute.For<IConnectDteService>();
        var inventario = new InventarioService(db, Substitute.For<IAuditoriaService>());
        return new PosService(db, Substitute.For<IAuditoriaService>(), new TicketPdfService(), email, dte, inventario, Options.Create(new PosOptions()));
    }

    private static CrearVentaRequest Venta(string forma) => new()
    {
        FormaPagoCodigo = forma, EfectivoRecibido = 100m,
        Lineas = [new CrearVentaLineaRequest { ProductoId = 1, Cantidad = 1m }],
    };

    [Fact]
    public async Task Abrir_CreaSesionAbierta()
    {
        var db = NewDb(); var caja = NewCaja(db);

        var r = await caja.AbrirAsync(Empresa, new AbrirCajaRequest { MontoInicial = 50m }, "cajero");

        r.IsSuccess.Should().BeTrue();
        r.Value!.EstadoCodigo.Should().Be("ABIERTA");
        r.Value.Numero.Should().Be("CAJA-000001");
        r.Value.MontoInicial.Should().Be(50m);
    }

    [Fact]
    public async Task Abrir_SegundaVez_Falla()
    {
        var db = NewDb(); var caja = NewCaja(db);
        await caja.AbrirAsync(Empresa, new AbrirCajaRequest { MontoInicial = 50m }, "c");

        var r = await caja.AbrirAsync(Empresa, new AbrirCajaRequest { MontoInicial = 10m }, "c");

        r.IsFailure.Should().BeTrue();
        r.ErrorCode.Should().Be("CAJA_ABIERTA");
    }

    [Fact]
    public async Task Estado_SinCaja_DevuelveNull()
    {
        var db = NewDb(); var caja = NewCaja(db);

        var r = await caja.GetEstadoAsync(Empresa);

        r.IsSuccess.Should().BeTrue();
        r.Value.Should().BeNull();
    }

    [Fact]
    public async Task Venta_SeLigaACajaAbierta_YEstadoSumaPorFormaPago()
    {
        var db = NewDb(); var caja = NewCaja(db); var pos = NewPos(db);
        await caja.AbrirAsync(Empresa, new AbrirCajaRequest { MontoInicial = 20m }, "c");

        await pos.CrearVentaAsync(Empresa, Venta("EFECTIVO"), "c"); // 11.30 efectivo
        await pos.CrearVentaAsync(Empresa, Venta("TARJETA"), "c");  // 11.30 tarjeta

        var estado = await caja.GetEstadoAsync(Empresa);
        estado.Value.Should().NotBeNull();
        estado.Value!.Ventas.Should().Be(2);
        estado.Value.TotalEfectivo.Should().Be(11.30m);
        estado.Value.TotalTarjeta.Should().Be(11.30m);
        estado.Value.EfectivoEsperado.Should().Be(31.30m); // 20 fondo + 11.30 efectivo
    }

    [Fact]
    public async Task Cerrar_Cuadrada_DiferenciaCero()
    {
        var db = NewDb(); var caja = NewCaja(db); var pos = NewPos(db);
        var abrir = await caja.AbrirAsync(Empresa, new AbrirCajaRequest { MontoInicial = 20m }, "c");
        await pos.CrearVentaAsync(Empresa, Venta("EFECTIVO"), "c"); // esperado 31.30

        var r = await caja.CerrarAsync(Empresa, abrir.Value!.Id, new CerrarCajaRequest { MontoContado = 31.30m }, "c");

        r.IsSuccess.Should().BeTrue();
        r.Value!.EstadoCodigo.Should().Be("CERRADA");
        r.Value.MontoEsperado.Should().Be(31.30m);
        r.Value.MontoContado.Should().Be(31.30m);
        r.Value.Diferencia.Should().Be(0m);
    }

    [Fact]
    public async Task Cerrar_ConFaltante_DiferenciaNegativa()
    {
        var db = NewDb(); var caja = NewCaja(db); var pos = NewPos(db);
        var abrir = await caja.AbrirAsync(Empresa, new AbrirCajaRequest { MontoInicial = 20m }, "c");
        await pos.CrearVentaAsync(Empresa, Venta("EFECTIVO"), "c"); // esperado 31.30

        var r = await caja.CerrarAsync(Empresa, abrir.Value!.Id, new CerrarCajaRequest { MontoContado = 30m }, "c");

        r.Value!.Diferencia.Should().Be(-1.30m);
    }

    [Fact]
    public async Task Cerrar_YaCerrada_Falla()
    {
        var db = NewDb(); var caja = NewCaja(db);
        var abrir = await caja.AbrirAsync(Empresa, new AbrirCajaRequest { MontoInicial = 20m }, "c");
        await caja.CerrarAsync(Empresa, abrir.Value!.Id, new CerrarCajaRequest { MontoContado = 20m }, "c");

        var r = await caja.CerrarAsync(Empresa, abrir.Value!.Id, new CerrarCajaRequest { MontoContado = 20m }, "c");

        r.IsFailure.Should().BeTrue();
        r.ErrorCode.Should().Be("INVALID_STATE");
    }
}
