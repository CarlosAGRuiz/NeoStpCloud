using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Common;
using NeoSTP.Application.Compras;
using NeoSTP.Application.Compras.Dtos;
using NeoSTP.Domain.Core.Compras;
using NeoSTP.Domain.Core.Empresas;
using NeoSTP.Domain.Core.Inventario;
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
        var auditoria = Substitute.For<IAuditoriaService>();
        var inventario = new InventarioService(db, auditoria);
        var correlativos = new NeoSTP.Infrastructure.Services.CorrelativoService(db);
        return (new OrdenCompraService(db, compras, inventario, auditoria, correlativos), compras);
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
        // Correlativo legible para el proveedor, no un fragmento de GUID.
        result.Value.Numero.Should().Be($"OC-{DateTime.UtcNow.Year}-000001");
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

    [Fact]
    public async Task Recibir_ParcialYLuegoCompleta_ActualizaEstadoEInventario()
    {
        var db = NewDb(); var (service, _) = NewService(db);
        var orden = await service.CrearAsync(Empresa, Request(), "admin");
        await service.EmitirAsync(Empresa, orden.Value!.Id, "admin");
        var lineaId = orden.Value.Detalle.Single().Id;

        var parcial = await service.RecibirAsync(Empresa, orden.Value.Id, Recepcion("recepcion-uno", lineaId, 1m), "admin");
        var completa = await service.RecibirAsync(Empresa, orden.Value.Id, Recepcion("recepcion-dos", lineaId, 1m), "admin");
        var detalle = await service.GetAsync(Empresa, orden.Value.Id);

        parcial.IsSuccess.Should().BeTrue();
        completa.IsSuccess.Should().BeTrue();
        detalle.Value!.EstadoCodigo.Should().Be(OrdenCompraEstados.Recibida);
        detalle.Value.Recepciones.Should().Be(2);
        detalle.Value.Detalle.Single().CantidadPendiente.Should().Be(0m);
        db.ExistenciasProducto.Single(x => x.ProductoId == 100).Cantidad.Should().Be(2m);
        db.MovimientosInventario.Should().HaveCount(2);
        db.MovimientosInventario.Should().OnlyContain(x => x.Origen == OrigenesMovimientoInventario.RecepcionCompra);
    }

    [Fact]
    public async Task Recibir_ExcedePendiente_NoModificaInventario()
    {
        var db = NewDb(); var (service, _) = NewService(db);
        var orden = await service.CrearAsync(Empresa, Request(), "admin");
        await service.EmitirAsync(Empresa, orden.Value!.Id, "admin");

        var result = await service.RecibirAsync(Empresa, orden.Value.Id,
            Recepcion("recepcion-excede", orden.Value.Detalle.Single().Id, 3m), "admin");

        result.ErrorCode.Should().Be("RECEPCION_EXCEDE_PENDIENTE");
        db.OrdenCompraRecepciones.Should().BeEmpty();
        db.MovimientosInventario.Should().BeEmpty();
    }

    [Fact]
    public async Task Recibir_MismaIdempotencyKey_NoDuplicaInventario()
    {
        var db = NewDb(); var (service, _) = NewService(db);
        var orden = await service.CrearAsync(Empresa, Request(), "admin");
        await service.EmitirAsync(Empresa, orden.Value!.Id, "admin");
        var request = Recepcion("recepcion-idempotente", orden.Value.Detalle.Single().Id, 1m);

        var first = await service.RecibirAsync(Empresa, orden.Value.Id, request, "admin");
        var second = await service.RecibirAsync(Empresa, orden.Value.Id, request, "admin");

        second.Value!.Id.Should().Be(first.Value!.Id);
        db.OrdenCompraRecepciones.Should().ContainSingle();
        db.MovimientosInventario.Should().ContainSingle();
        db.ExistenciasProducto.Single().Cantidad.Should().Be(1m);
    }

    [Fact]
    public async Task ConvertirAFactura_TrasRecepciones_NoDuplicaEntradaInventario()
    {
        var db = NewDb(); var (service, compras) = NewService(db);
        var orden = await service.CrearAsync(Empresa, Request(), "admin");
        await service.EmitirAsync(Empresa, orden.Value!.Id, "admin");
        await service.RecibirAsync(Empresa, orden.Value.Id,
            Recepcion("recepcion-completa", orden.Value.Detalle.Single().Id, 2m), "admin");

        var result = await service.ConvertirAFacturaAsync(Empresa, orden.Value.Id,
            new ConvertirOrdenCompraRequest { NumeroDocumento = "F-REC-1" }, "admin");

        result.IsSuccess.Should().BeTrue();
        db.MovimientosInventario.Should().ContainSingle();
        await compras.Received(1).CrearFacturaAsync(Empresa,
            Arg.Is<CrearFacturaCompraRequest>(x => x.LineasInventario == null),
            "admin", Arg.Any<CancellationToken>());
    }

    // ── E4: aprobación de órdenes por monto ──────────────────────────────────

    [Fact]
    public async Task Emitir_SobreUmbral_EnviaAPorAprobar()
    {
        var db = NewDb(); var (service, _) = NewService(db);
        (await service.SetUmbralAprobacionAsync(Empresa, 100m, "admin")).IsSuccess.Should().BeTrue();
        var orden = await service.CrearAsync(Empresa, Request(), "admin"); // total 226 ≥ 100

        var result = await service.EmitirAsync(Empresa, orden.Value!.Id, "admin");

        result.IsSuccess.Should().BeTrue();
        result.Value!.EstadoCodigo.Should().Be(OrdenCompraEstados.PorAprobar);
    }

    [Fact]
    public async Task Emitir_BajoUmbral_EmiteDirecto()
    {
        var db = NewDb(); var (service, _) = NewService(db);
        await service.SetUmbralAprobacionAsync(Empresa, 500m, "admin");
        var orden = await service.CrearAsync(Empresa, Request(), "admin"); // total 226 < 500

        var result = await service.EmitirAsync(Empresa, orden.Value!.Id, "admin");

        result.Value!.EstadoCodigo.Should().Be(OrdenCompraEstados.Emitida);
    }

    [Fact]
    public async Task Emitir_SinUmbral_EmiteDirecto()
    {
        var db = NewDb(); var (service, _) = NewService(db);
        var orden = await service.CrearAsync(Empresa, Request(), "admin");

        var result = await service.EmitirAsync(Empresa, orden.Value!.Id, "admin");

        result.Value!.EstadoCodigo.Should().Be(OrdenCompraEstados.Emitida);
    }

    [Fact]
    public async Task Aprobar_PorAprobar_PasaAEmitida()
    {
        var db = NewDb(); var (service, _) = NewService(db);
        await service.SetUmbralAprobacionAsync(Empresa, 100m, "admin");
        var orden = await service.CrearAsync(Empresa, Request(), "admin");
        await service.EmitirAsync(Empresa, orden.Value!.Id, "admin");

        var result = await service.AprobarAsync(Empresa, orden.Value.Id, "jefe");

        result.IsSuccess.Should().BeTrue();
        result.Value!.EstadoCodigo.Should().Be(OrdenCompraEstados.Emitida);
    }

    [Fact]
    public async Task Aprobar_OrdenEnBorrador_Falla()
    {
        var db = NewDb(); var (service, _) = NewService(db);
        var orden = await service.CrearAsync(Empresa, Request(), "admin");

        var result = await service.AprobarAsync(Empresa, orden.Value!.Id, "jefe");

        result.ErrorCode.Should().Be("INVALID_STATE");
    }

    [Fact]
    public async Task Rechazar_PorAprobar_RegresaABorradorConMotivo()
    {
        var db = NewDb(); var (service, _) = NewService(db);
        await service.SetUmbralAprobacionAsync(Empresa, 100m, "admin");
        var orden = await service.CrearAsync(Empresa, Request(), "admin");
        await service.EmitirAsync(Empresa, orden.Value!.Id, "admin");

        var result = await service.RechazarAsync(Empresa, orden.Value.Id, "excede presupuesto", "jefe");

        result.IsSuccess.Should().BeTrue();
        result.Value!.EstadoCodigo.Should().Be(OrdenCompraEstados.Borrador);
        result.Value.Observaciones.Should().Contain("excede presupuesto");
    }

    [Fact]
    public async Task Rechazar_OrdenEnBorrador_Falla()
    {
        var db = NewDb(); var (service, _) = NewService(db);
        var orden = await service.CrearAsync(Empresa, Request(), "admin");

        var result = await service.RechazarAsync(Empresa, orden.Value!.Id, "motivo", "jefe");

        result.ErrorCode.Should().Be("INVALID_STATE");
    }

    [Fact]
    public async Task Emitir_SobreUmbral_EmiteWebhookDeNegocio()
    {
        var db = NewDb();
        var compras = Substitute.For<ICompraService>();
        var auditoria = Substitute.For<IAuditoriaService>();
        var webhooks = Substitute.For<NeoSTP.Application.Connect.IConnectWebhookDispatcher>();
        var service = new OrdenCompraService(db, compras, new InventarioService(db, auditoria), auditoria,
            new NeoSTP.Infrastructure.Services.CorrelativoService(db), webhooks);

        await service.SetUmbralAprobacionAsync(Empresa, 100m, "admin");
        var orden = await service.CrearAsync(Empresa, Request(), "admin"); // total 226

        await service.EmitirAsync(Empresa, orden.Value!.Id, "admin");

        await webhooks.Received(1).DispatchNegocioAsync(
            Arg.Is<NeoSTP.Application.Connect.ConnectEventoNegocioPayload>(p =>
                p.Evento == NeoSTP.Domain.Core.Connect.ConnectEventos.CompraOrdenPorAprobar
                && p.EmpresaId == Empresa
                && p.EntidadTipo == "OrdenCompra"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Emitir_BajoUmbral_NoMolestaAlIntegrador()
    {
        var db = NewDb();
        var compras = Substitute.For<ICompraService>();
        var auditoria = Substitute.For<IAuditoriaService>();
        var webhooks = Substitute.For<NeoSTP.Application.Connect.IConnectWebhookDispatcher>();
        var service = new OrdenCompraService(db, compras, new InventarioService(db, auditoria), auditoria,
            new NeoSTP.Infrastructure.Services.CorrelativoService(db), webhooks);

        var orden = await service.CrearAsync(Empresa, Request(), "admin"); // sin umbral
        await service.EmitirAsync(Empresa, orden.Value!.Id, "admin");

        await webhooks.DidNotReceive().DispatchNegocioAsync(
            Arg.Any<NeoSTP.Application.Connect.ConnectEventoNegocioPayload>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SetUmbral_Negativo_Falla()
    {
        var db = NewDb(); var (service, _) = NewService(db);

        var result = await service.SetUmbralAprobacionAsync(Empresa, -1m, "admin");

        result.ErrorCode.Should().Be("VALIDATION");
    }

    [Fact]
    public async Task SetUmbral_Cero_DesactivaAprobacion()
    {
        var db = NewDb(); var (service, _) = NewService(db);
        await service.SetUmbralAprobacionAsync(Empresa, 100m, "admin");

        var result = await service.SetUmbralAprobacionAsync(Empresa, 0m, "admin");

        result.IsSuccess.Should().BeTrue();
        db.Empresas.Single(e => e.Id == Empresa).UmbralAprobacionCompras.Should().BeNull();
    }

    private static RegistrarRecepcionOrdenCompraRequest Recepcion(
        string key, int lineaId, decimal cantidad) => new()
    {
        IdempotencyKey = key,
        Fecha = new DateOnly(2026, 6, 20),
        Referencia = "ENT-001",
        Lineas = [new RegistrarRecepcionOrdenCompraLineaRequest
        {
            OrdenCompraLineaId = lineaId,
            Cantidad = cantidad,
        }],
    };
}

internal static class OrdenCompraTestExtensions
{
    public static decimal TotalEsperado(this CrearFacturaCompraRequest request) => request.Subtotal + request.Iva;
}
