using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Common;
using NeoSTP.Application.Comunicaciones;
using NeoSTP.Application.Connect;
using NeoSTP.Application.Dte.Abstractions;
using NeoSTP.Application.Dte.Dtos;
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

/// <summary>NEOPOS — PosService: ventas, totales, numeración, ticket y envío.</summary>
public class PosServiceTests
{
    private const int Empresa = 42;

    private static NeoStpDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<NeoStpDbContext>()
            .UseInMemoryDatabase($"pos-{Guid.NewGuid()}").Options;
        var db = new NeoStpDbContext(options);
        db.Empresas.Add(new Empresa { Id = Empresa, Nit = "123", Nrc = "NRC1", RazonSocial = "Mi Tienda", EstadoCodigo = "ACTIVA" });
        db.Productos.Add(new Producto
        {
            Id = 1, EmpresaId = Empresa, CodigoInterno = "P1", Nombre = "Café", PrecioUnitario = 11.30m,
            AplicaIva = true, EstadoCodigo = "ACTIVO", UnidadMedidaCodigo = "59", TipoItem = "BIEN",
        });
        db.SaveChanges();
        return db;
    }

    private static (PosService svc, ITenantEmailSender email, IConnectDteService dte) NewSvc(NeoStpDbContext db)
    {
        var email = Substitute.For<ITenantEmailSender>();
        email.EnviarAsync(Arg.Any<int>(), Arg.Any<EmailMessage>(), Arg.Any<CancellationToken>())
            .Returns(new EmailSendResult { Success = true, MessageId = "ok" });
        var dte = Substitute.For<IConnectDteService>();
        var svc = new PosService(db, Substitute.For<IAuditoriaService>(), new TicketPdfService(), email, dte, Options.Create(new PosOptions()));
        return (svc, email, dte);
    }

    private static CrearVentaRequest VentaConProducto(decimal cant = 2m) => new()
    {
        FormaPagoCodigo = "EFECTIVO", EfectivoRecibido = 50m,
        Lineas = [new CrearVentaLineaRequest { ProductoId = 1, Cantidad = cant }],
    };

    [Fact]
    public async Task CrearVenta_CalculaTotalesYNumeroYCambio()
    {
        var db = NewDb(); var (svc, _, _) = NewSvc(db);

        var r = await svc.CrearVentaAsync(Empresa, VentaConProducto(2m), "cajero");

        r.IsSuccess.Should().BeTrue();
        r.Value!.Numero.Should().Be("POS-000001");
        r.Value.Total.Should().Be(22.60m);
        r.Value.IvaTotal.Should().Be(2.60m);
        r.Value.Subtotal.Should().Be(20.00m);
        r.Value.Cambio.Should().Be(27.40m); // 50 - 22.60
        r.Value.EstadoCodigo.Should().Be("COMPLETADA");
        r.Value.Lineas.Should().ContainSingle();
        r.Value.Lineas[0].Descripcion.Should().Be("Café");
    }

    [Fact]
    public async Task CrearVenta_NumeracionCorrelativa()
    {
        var db = NewDb(); var (svc, _, _) = NewSvc(db);
        await svc.CrearVentaAsync(Empresa, VentaConProducto(), "c");

        var r2 = await svc.CrearVentaAsync(Empresa, VentaConProducto(), "c");

        r2.Value!.Numero.Should().Be("POS-000002");
    }

    [Fact]
    public async Task CrearVenta_SinLineas_Falla()
    {
        var db = NewDb(); var (svc, _, _) = NewSvc(db);

        var r = await svc.CrearVentaAsync(Empresa, new CrearVentaRequest { FormaPagoCodigo = "EFECTIVO", Lineas = [] }, "c");

        r.IsFailure.Should().BeTrue();
        r.ErrorCode.Should().Be("VALIDATION");
    }

    [Fact]
    public async Task CrearVenta_ItemManualSinPrecio_Falla()
    {
        var db = NewDb(); var (svc, _, _) = NewSvc(db);

        var r = await svc.CrearVentaAsync(Empresa, new CrearVentaRequest
        { FormaPagoCodigo = "EFECTIVO", Lineas = [new CrearVentaLineaRequest { Descripcion = "X", Cantidad = 1m }] }, "c");

        r.IsFailure.Should().BeTrue();
        r.ErrorCode.Should().Be("VALIDATION");
    }

    [Fact]
    public async Task Anular_DejaAnulada()
    {
        var db = NewDb(); var (svc, _, _) = NewSvc(db);
        var v = await svc.CrearVentaAsync(Empresa, VentaConProducto(), "c");

        var r = await svc.AnularAsync(Empresa, v.Value!.Id, "c");

        r.IsSuccess.Should().BeTrue();
        (await db.VentasPos.FirstAsync()).EstadoCodigo.Should().Be("ANULADA");
    }

    [Fact]
    public async Task GetTicket_TraeDatosEmpresaYLineas()
    {
        var db = NewDb(); var (svc, _, _) = NewSvc(db);
        var v = await svc.CrearVentaAsync(Empresa, VentaConProducto(), "c");

        var t = await svc.GetTicketAsync(Empresa, v.Value!.Id);

        t.IsSuccess.Should().BeTrue();
        t.Value!.EmpresaNombre.Should().Be("Mi Tienda");
        t.Value.EmpresaNit.Should().Be("123");
        t.Value.Lineas.Should().ContainSingle();
        t.Value.Total.Should().Be(22.60m);
    }

    [Fact]
    public async Task EnviarTicket_AdjuntaPdfYLlamaEmail()
    {
        var db = NewDb(); var (svc, email, _) = NewSvc(db);
        var v = await svc.CrearVentaAsync(Empresa, VentaConProducto(), "c");

        var r = await svc.EnviarTicketCorreoAsync(Empresa, v.Value!.Id, "cliente@x.com", "c");

        r.IsSuccess.Should().BeTrue();
        await email.Received(1).EnviarAsync(Empresa,
            Arg.Is<EmailMessage>(m => m.To == "cliente@x.com" && m.Attachments.Count == 1 && m.Attachments[0].Content.Length > 500),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ResumenDia_AgrupaPorFormaPago()
    {
        var db = NewDb(); var (svc, _, _) = NewSvc(db);
        await svc.CrearVentaAsync(Empresa, VentaConProducto(), "c"); // efectivo 22.60
        await svc.CrearVentaAsync(Empresa, new CrearVentaRequest
        { FormaPagoCodigo = "TARJETA", Lineas = [new CrearVentaLineaRequest { ProductoId = 1, Cantidad = 1m }] }, "c"); // 11.30

        var r = await svc.ResumenDiaAsync(Empresa, DateOnly.FromDateTime(DateTime.UtcNow));

        r.IsSuccess.Should().BeTrue();
        r.Value!.Ventas.Should().Be(2);
        r.Value.Total.Should().Be(33.90m);
        r.Value.Efectivo.Should().Be(22.60m);
        r.Value.Tarjeta.Should().Be(11.30m);
    }

    [Fact]
    public async Task PromoverADte_Exito_EnlazaYMarcaFacturada()
    {
        var db = NewDb(); var (svc, _, dte) = NewSvc(db);
        var v = await svc.CrearVentaAsync(Empresa, VentaConProducto(), "c");
        dte.EmitirAsync(Empresa, Arg.Any<CreateDteDocumentoRequest>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result<DteDocumentoDto>.Ok(new DteDocumentoDto { Id = 777, EstadoCodigo = "PROCESADO" }));

        var r = await svc.PromoverADteAsync(Empresa, v.Value!.Id, new PromoverVentaRequest { TipoDteCodigo = "01" }, "c");

        r.IsSuccess.Should().BeTrue();
        r.Value!.EstadoFacturacion.Should().Be("FACTURADA");
        r.Value.DteDocumentoId.Should().Be(777);
        await dte.Received(1).EmitirAsync(Empresa,
            Arg.Is<CreateDteDocumentoRequest>(req => req.TipoDteCodigo == "01" && req.Lineas.Count == 1 && req.CondicionOperacionCodigo == "1"),
            "c", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PromoverADte_FallaEmision_NoMarcaFacturada()
    {
        var db = NewDb(); var (svc, _, dte) = NewSvc(db);
        var v = await svc.CrearVentaAsync(Empresa, VentaConProducto(), "c");
        dte.EmitirAsync(Empresa, Arg.Any<CreateDteDocumentoRequest>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result<DteDocumentoDto>.Fail("Certificado no cargado.", "VALIDATION"));

        var r = await svc.PromoverADteAsync(Empresa, v.Value!.Id, new PromoverVentaRequest(), "c");

        r.IsFailure.Should().BeTrue();
        (await db.VentasPos.FirstAsync()).EstadoFacturacion.Should().Be("NO_FACTURADA");
    }

    [Fact]
    public async Task PromoverADte_CcfSinCliente_Falla()
    {
        var db = NewDb(); var (svc, _, _) = NewSvc(db);
        var v = await svc.CrearVentaAsync(Empresa, VentaConProducto(), "c");

        var r = await svc.PromoverADteAsync(Empresa, v.Value!.Id, new PromoverVentaRequest { TipoDteCodigo = "03" }, "c");

        r.IsFailure.Should().BeTrue();
        r.ErrorCode.Should().Be("VALIDATION");
    }

    [Fact]
    public void TicketPdf_GeneraPdfValido()
    {
        var pdf = new TicketPdfService().GenerarTicket(new TicketModel
        {
            EmpresaNombre = "Mi Tienda", Numero = "POS-000001", Fecha = DateTime.UtcNow,
            Total = 22.60m, Subtotal = 20m, IvaTotal = 2.60m,
            Lineas = [new TicketLinea { Descripcion = "Café", Cantidad = 2, PrecioUnitario = 11.30m, Total = 22.60m }],
        });

        pdf.Should().NotBeNullOrEmpty();
        pdf.Length.Should().BeGreaterThan(500);
        System.Text.Encoding.ASCII.GetString(pdf, 0, 4).Should().Be("%PDF");
    }
}
