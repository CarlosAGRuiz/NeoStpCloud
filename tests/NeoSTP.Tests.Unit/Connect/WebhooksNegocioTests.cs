using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Connect;
using NeoSTP.Domain.Core.Connect;
using NeoSTP.Infrastructure.Persistence;
using NeoSTP.Infrastructure.Services;
using NSubstitute;
using Xunit;

namespace NeoSTP.Tests.Unit.Connect;

/// <summary>
/// E6: el integrador no solo quiere saber de facturas. Estos eventos de negocio viajan
/// por el mismo transporte con reintentos y firma que los de DTE.
/// </summary>
public class WebhooksNegocioTests
{
    private const int Empresa = 1;

    private static NeoStpDbContext NewDb() => new(
        new DbContextOptionsBuilder<NeoStpDbContext>()
            .UseInMemoryDatabase($"whneg-{Guid.NewGuid()}").Options);

    private static ConnectWebhookDispatcher NewDispatcher(NeoStpDbContext db) => new(
        db, Substitute.For<IHttpClientFactory>(), NullLogger<ConnectWebhookDispatcher>.Instance);

    private static void SeedWebhook(NeoStpDbContext db, string eventos, bool activo = true)
    {
        db.ConnectWebhooks.Add(new ConnectWebhook
        {
            EmpresaId = Empresa, Url = "https://cliente.demo/hook",
            SecretoHmac = "secreto", Eventos = eventos, Activo = activo,
        });
        db.SaveChanges();
    }

    private static ConnectEventoNegocioPayload Payload(string evento) => new()
    {
        Evento = evento,
        EmpresaId = Empresa,
        EntidadTipo = "PagoCliente",
        EntidadId = 7,
        Descripcion = "Pago confirmado",
        Datos = new Dictionary<string, object?> { ["monto"] = 113.00m, ["saldado"] = true },
    };

    [Fact]
    public async Task EventoDeNegocio_EncolaEntregaAlSuscrito()
    {
        await using var db = NewDb();
        SeedWebhook(db, ConnectEventos.CobroPagoConfirmado);

        await NewDispatcher(db).DispatchNegocioAsync(Payload(ConnectEventos.CobroPagoConfirmado));

        var entrega = await db.ConnectWebhookDeliveries.SingleAsync();
        entrega.Evento.Should().Be(ConnectEventos.CobroPagoConfirmado);
        entrega.Estado.Should().Be(ConnectDeliveryEstados.Pendiente);
        entrega.Payload.Should().Contain("monto").And.Contain("113");
    }

    [Fact]
    public async Task NoSuscritoAlEvento_NoRecibeNada()
    {
        await using var db = NewDb();
        SeedWebhook(db, ConnectEventos.DteProcesado);

        await NewDispatcher(db).DispatchNegocioAsync(Payload(ConnectEventos.CobroPagoConfirmado));

        db.ConnectWebhookDeliveries.Should().BeEmpty();
    }

    [Fact]
    public async Task WebhookInactivo_NoRecibeNada()
    {
        await using var db = NewDb();
        SeedWebhook(db, ConnectEventos.CobroPagoConfirmado, activo: false);

        await NewDispatcher(db).DispatchNegocioAsync(Payload(ConnectEventos.CobroPagoConfirmado));

        db.ConnectWebhookDeliveries.Should().BeEmpty();
    }

    [Fact]
    public async Task OtraEmpresa_NoRecibeElEvento()
    {
        await using var db = NewDb();
        db.ConnectWebhooks.Add(new ConnectWebhook
        {
            EmpresaId = 99, Url = "https://otra.demo/hook",
            SecretoHmac = "s", Eventos = ConnectEventos.CobroPagoConfirmado, Activo = true,
        });
        await db.SaveChangesAsync();

        await NewDispatcher(db).DispatchNegocioAsync(Payload(ConnectEventos.CobroPagoConfirmado));

        db.ConnectWebhookDeliveries.Should().BeEmpty();
    }

    [Fact]
    public async Task VariosSuscritos_CadaUnoRecibeSuEntrega()
    {
        await using var db = NewDb();
        SeedWebhook(db, ConnectEventos.CobroPagoConfirmado);
        SeedWebhook(db, $"{ConnectEventos.DteProcesado},{ConnectEventos.CobroPagoConfirmado}");

        await NewDispatcher(db).DispatchNegocioAsync(Payload(ConnectEventos.CobroPagoConfirmado));

        (await db.ConnectWebhookDeliveries.CountAsync()).Should().Be(2);
    }

    [Theory]
    [InlineData(ConnectEventos.CobroPagoConfirmado)]
    [InlineData(ConnectEventos.CompraOrdenPorAprobar)]
    [InlineData(ConnectEventos.InventarioStockBajo)]
    [InlineData(ConnectEventos.AgendaCitaCreada)]
    public void EventosDeNegocio_EstanPublicadosYDescritos(string evento)
    {
        ConnectEventos.All.Should().Contain(evento);
        ConnectEventos.Negocio.Should().Contain(evento);
        // La UI de suscripción muestra esta descripción: no debe quedar el código crudo.
        ConnectEventos.Describir(evento).Should().NotBe(evento);
    }

    [Fact]
    public void EventosDte_SiguenPublicadosYFueraDeLosDeNegocio()
    {
        ConnectEventos.All.Should().Contain(ConnectEventos.DteProcesado);
        ConnectEventos.Negocio.Should().NotContain(ConnectEventos.DteProcesado);
    }
}
