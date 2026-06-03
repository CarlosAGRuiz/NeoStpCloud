using System.Net;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NeoSTP.Application.Connect;
using NeoSTP.Domain.Core.Connect;
using NeoSTP.Infrastructure.Persistence;
using NeoSTP.Infrastructure.Services;
using NSubstitute;
using Xunit;

namespace NeoSTP.Tests.Unit.Connect;

/// <summary>
/// NeoConnect — dispatcher de webhooks: creación de entregas para suscritos y
/// reintentos con backoff exponencial / marcado como fallido tras el máximo.
/// </summary>
public class ConnectWebhookDispatcherTests
{
    private const int EmpresaA = 3;
    private const int EmpresaB = 4;

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _status;
        public StubHandler(HttpStatusCode status) => _status = status;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            => Task.FromResult(new HttpResponseMessage(_status));
    }

    private static NeoStpDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<NeoStpDbContext>()
            .UseInMemoryDatabase($"connect-wh-{Guid.NewGuid()}")
            .Options;
        return new NeoStpDbContext(options);
    }

    private static ConnectWebhookDispatcher NewDispatcher(NeoStpDbContext db, HttpStatusCode status)
    {
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(Arg.Any<string>()).Returns(_ => new HttpClient(new StubHandler(status)));
        return new ConnectWebhookDispatcher(db, factory, NullLogger<ConnectWebhookDispatcher>.Instance);
    }

    private static ConnectDteEventoPayload Payload(int empresa, string evento) => new()
    {
        Evento = evento, EmpresaId = empresa, DteId = 1,
        CodigoGeneracion = "ABC", TipoDte = "01", Estado = "PROCESADO",
    };

    [Fact]
    public async Task Dispatch_CreaEntregasSoloParaWebhooksSuscritos()
    {
        var db = NewDb();
        db.ConnectWebhooks.AddRange(
            new ConnectWebhook { Id = 1, EmpresaId = EmpresaA, Url = "https://a/hook", SecretoHmac = "s1", Eventos = ConnectEventos.DteProcesado, Activo = true },
            new ConnectWebhook { Id = 2, EmpresaId = EmpresaA, Url = "https://b/hook", SecretoHmac = "s2", Eventos = ConnectEventos.DteRechazado, Activo = true },
            new ConnectWebhook { Id = 3, EmpresaId = EmpresaA, Url = "https://c/hook", SecretoHmac = "s3", Eventos = ConnectEventos.DteProcesado, Activo = false },
            new ConnectWebhook { Id = 4, EmpresaId = EmpresaB, Url = "https://d/hook", SecretoHmac = "s4", Eventos = ConnectEventos.DteProcesado, Activo = true });
        await db.SaveChangesAsync();
        var disp = NewDispatcher(db, HttpStatusCode.OK);

        await disp.DispatchAsync(Payload(EmpresaA, ConnectEventos.DteProcesado));

        var deliveries = await db.ConnectWebhookDeliveries.ToListAsync();
        deliveries.Should().HaveCount(1);
        deliveries[0].WebhookId.Should().Be(1);
        deliveries[0].Estado.Should().Be(ConnectDeliveryEstados.Pendiente);
    }

    [Fact]
    public async Task Dispatch_SinSuscritos_NoCreaEntregas()
    {
        var db = NewDb();
        db.ConnectWebhooks.Add(new ConnectWebhook
        {
            Id = 1, EmpresaId = EmpresaA, Url = "https://a/hook", SecretoHmac = "s1",
            Eventos = ConnectEventos.DteRechazado, Activo = true,
        });
        await db.SaveChangesAsync();
        var disp = NewDispatcher(db, HttpStatusCode.OK);

        await disp.DispatchAsync(Payload(EmpresaA, ConnectEventos.DteProcesado));

        (await db.ConnectWebhookDeliveries.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Procesar_RespuestaNo2xx_ReintentaConBackoff()
    {
        var db = NewDb();
        db.ConnectWebhooks.Add(new ConnectWebhook { Id = 1, EmpresaId = EmpresaA, Url = "https://x/hook", SecretoHmac = "secret", Eventos = ConnectEventos.DteProcesado, Activo = true });
        db.ConnectWebhookDeliveries.Add(new ConnectWebhookDelivery
        {
            Id = 10, WebhookId = 1, EmpresaId = EmpresaA, Evento = ConnectEventos.DteProcesado,
            Payload = "{}", Estado = ConnectDeliveryEstados.Pendiente, Intentos = 0,
            ProximoIntento = DateTime.UtcNow.AddMinutes(-1),
        });
        await db.SaveChangesAsync();
        var disp = NewDispatcher(db, HttpStatusCode.InternalServerError);

        var procesados = await disp.ProcesarPendientesAsync();

        procesados.Should().Be(1);
        var d = await db.ConnectWebhookDeliveries.AsNoTracking().SingleAsync();
        d.Intentos.Should().Be(1);
        d.Estado.Should().Be(ConnectDeliveryEstados.Pendiente);
        d.HttpStatus.Should().Be(500);
        d.ProximoIntento.Should().BeAfter(DateTime.UtcNow.AddMinutes(1)); // backoff ~2 min
    }

    [Fact]
    public async Task Procesar_TrasMaxIntentos_MarcaFallido()
    {
        var db = NewDb();
        db.ConnectWebhooks.Add(new ConnectWebhook { Id = 1, EmpresaId = EmpresaA, Url = "https://x/hook", SecretoHmac = "secret", Eventos = ConnectEventos.DteProcesado, Activo = true });
        db.ConnectWebhookDeliveries.Add(new ConnectWebhookDelivery
        {
            Id = 11, WebhookId = 1, EmpresaId = EmpresaA, Evento = ConnectEventos.DteProcesado,
            Payload = "{}", Estado = ConnectDeliveryEstados.Pendiente, Intentos = 4,
            ProximoIntento = DateTime.UtcNow.AddMinutes(-1),
        });
        await db.SaveChangesAsync();
        var disp = NewDispatcher(db, HttpStatusCode.InternalServerError);

        await disp.ProcesarPendientesAsync();

        var d = await db.ConnectWebhookDeliveries.AsNoTracking().SingleAsync();
        d.Intentos.Should().Be(5);
        d.Estado.Should().Be(ConnectDeliveryEstados.Fallido);
    }
}
