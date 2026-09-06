using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Comunicaciones;
using NeoSTP.Application.Connect;
using NeoSTP.Application.Dte;
using NeoSTP.Application.Dte.Abstractions;
using NeoSTP.Domain.Core.Connect;
using NeoSTP.Domain.Core.Dte;
using NeoSTP.Domain.Core.Empresas;
using NeoSTP.Infrastructure.Dte;
using NeoSTP.Infrastructure.Persistence;
using NeoSTP.Infrastructure.Services;
using NSubstitute;

namespace NeoSTP.Tests.Unit.Dte;

public class DteReconciliationTests
{
    [Fact]
    public async Task HttpClient_SendsOnlyPersistedIdentityAndParsesLegacyReceiptName()
    {
        var codigo = Guid.NewGuid().ToString("D").ToUpperInvariant();
        using var handler = new CaptureHandler(HttpStatusCode.OK, JsonSerializer.Serialize(new
        {
            version = 1, ambiente = "00", numValidacion = "SELLO-SYNTHETIC",
            codigoGeneracion = $" {codigo}", codigoMsg = "001",
            descripcionMsg = "DTE RECIBIDO, VALIDADO Y PROCESADO"
        }));
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(HttpHaciendaConsultaDteClient.HttpClientName)
            .Returns(new HttpClient(handler, disposeHandler: false));
        var client = new HttpHaciendaConsultaDteClient(factory,
            Options.Create(new HaciendaOptions { PruebasBaseUrl = "https://synthetic.invalid" }),
            NullLogger<HttpHaciendaConsultaDteClient>.Instance);

        var result = await client.ConsultarAsync(new()
        {
            Ambiente = "00", AmbienteCodigo = DteAmbientes.Pruebas,
            NitEmisor = "00000000000000", TipoDte = "01", CodigoGeneracion = codigo, Token = "synthetic-token"
        });

        result.Success.Should().BeTrue();
        result.SelloRecibido.Should().Be("SELLO-SYNTHETIC");
        result.CodigoGeneracion.Should().Be(codigo);
        handler.Uri.Should().Be("https://synthetic.invalid/fesv/recepcion/consultadte/");
        handler.Authorization.Should().Be("Bearer synthetic-token");
        using var body = JsonDocument.Parse(handler.Body!);
        body.RootElement.EnumerateObject().Select(x => x.Name)
            .Should().BeEquivalentTo("nitEmisor", "tdte", "codigoGeneracion");
        body.RootElement.GetProperty("codigoGeneracion").GetString().Should().Be(codigo);
    }

    [Fact]
    public async Task HttpClient_InvalidIdentityDoesNotCreateHttpClient()
    {
        var factory = Substitute.For<IHttpClientFactory>();
        var client = new HttpHaciendaConsultaDteClient(factory, Options.Create(new HaciendaOptions()),
            NullLogger<HttpHaciendaConsultaDteClient>.Instance);

        var result = await client.ConsultarAsync(new()
        {
            Ambiente = "01", AmbienteCodigo = DteAmbientes.Pruebas,
            NitEmisor = "invalid", TipoDte = "1", CodigoGeneracion = "invalid", Token = ""
        });

        result.Success.Should().BeFalse();
        result.CodigoMsg.Should().Be("DTE_CONSULTA_INCOMPATIBLE");
        factory.ReceivedCalls().Should().BeEmpty();
    }

    [Fact]
    public async Task ExactConfirmation_ProcessesWithoutReceptionAndPreservesOriginalEvidence()
    {
        await using var db = DteFiscalIsolationTests.Db();
        var doc = await Seed(db);
        var originalEvidence = doc.Json!.RespuestaHacienda;
        var query = Substitute.For<IHaciendaConsultaDteClient>();
        query.ConsultarAsync(Arg.Any<HaciendaConsultaDteRequest>(), Arg.Any<CancellationToken>())
            .Returns(call => Confirmed(call.Arg<HaciendaConsultaDteRequest>()));
        var reception = Substitute.For<IHaciendaReceptionClient>();
        var webhooks = Substitute.For<IConnectWebhookDispatcher>();
        var service = Service(db, query, reception, webhooks);

        var result = await service.ConciliarHaciendaAsync(10, doc.Id, "auditor");

        result.IsSuccess.Should().BeTrue();
        result.Value!.EstadoCodigo.Should().Be(DteEstadoCodigos.Procesado);
        result.Value.SelloRecibido.Should().Be("SELLO-CONSULTA");
        result.Value.RespuestaHacienda.Should().Be(originalEvidence, "la consulta no reemplaza la evidencia del envío original");
        var occurrence = await db.DteErrorOcurrencias.SingleAsync(x => x.DteDocumentoId == doc.Id);
        occurrence.CodigoError.Should().Be("CONSULTA_CONFIRMADA");
        occurrence.Resuelta.Should().BeTrue();
        occurrence.RespuestaMhJson.Should().Contain("SELLO-CONSULTA");
        await query.Received(1).ConsultarAsync(Arg.Is<HaciendaConsultaDteRequest>(x =>
            x.NitEmisor == "00000000000000" && x.Ambiente == "00"
            && x.TipoDte == doc.TipoDteCodigo && x.CodigoGeneracion == doc.CodigoGeneracion),
            Arg.Any<CancellationToken>());
        await reception.DidNotReceive().EnviarAsync(Arg.Any<HaciendaReceptionRequest>(), Arg.Any<CancellationToken>());
        await webhooks.Received(1).DispatchAsync(Arg.Is<ConnectDteEventoPayload>(x =>
            x.Evento == ConnectEventos.DteProcesado && x.DteId == doc.Id), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("uuid")]
    [InlineData("ambiente")]
    [InlineData("sello")]
    [InlineData("codigo")]
    [InlineData("estado")]
    public async Task IncompatibleResponse_PreservesUncertainAttempt(string mismatch)
    {
        await using var db = DteFiscalIsolationTests.Db();
        var doc = await Seed(db);
        var originalJws = doc.Json!.JsonFirmado;
        var originalEvidence = doc.Json.RespuestaHacienda;
        var query = Substitute.For<IHaciendaConsultaDteClient>();
        query.ConsultarAsync(Arg.Any<HaciendaConsultaDteRequest>(), Arg.Any<CancellationToken>())
            .Returns(call => Mismatch(call.Arg<HaciendaConsultaDteRequest>(), mismatch));
        var webhooks = Substitute.For<IConnectWebhookDispatcher>();
        var service = Service(db, query, Substitute.For<IHaciendaReceptionClient>(), webhooks);

        var result = await service.ConciliarHaciendaAsync(10, doc.Id, "auditor");

        result.ErrorCode.Should().Be("DTE_RESULTADO_INCIERTO");
        db.ChangeTracker.Clear();
        var actual = await db.DteDocumentos.Include(x => x.Json).SingleAsync(x => x.Id == doc.Id);
        actual.EstadoCodigo.Should().Be(DteEstadoCodigos.Enviado);
        actual.SelloRecibido.Should().BeNull();
        actual.Json!.JsonFirmado.Should().Be(originalJws);
        actual.Json.RespuestaHacienda.Should().Be(originalEvidence);
        (await db.DteErrorOcurrencias.SingleAsync(x => x.DteDocumentoId == doc.Id)).Resuelta.Should().BeFalse();
        await webhooks.DidNotReceive().DispatchAsync(Arg.Any<ConnectDteEventoPayload>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LegacyAttemptWithoutTimestamp_IsNotQueriedOrAuthenticated()
    {
        await using var db = DteFiscalIsolationTests.Db();
        var doc = await Seed(db);
        doc.EnviadoAt = null;
        await db.SaveChangesAsync();
        var query = Substitute.For<IHaciendaConsultaDteClient>();
        var auth = Substitute.For<IHaciendaAuthClient>();
        var service = Service(db, query, Substitute.For<IHaciendaReceptionClient>(), authOverride: auth);

        var result = await service.ConciliarHaciendaAsync(10, doc.Id, "auditor");

        result.ErrorCode.Should().Be("DTE_CONSULTA_NO_DISPONIBLE");
        await query.DidNotReceive().ConsultarAsync(Arg.Any<HaciendaConsultaDteRequest>(), Arg.Any<CancellationToken>());
        await auth.DidNotReceive().AutenticarAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SignedNitFromAnotherCompany_IsRejectedBeforeAuthenticationOrHttp()
    {
        await using var db = DteFiscalIsolationTests.Db();
        var doc = await Seed(db);
        var foreignPayload = JsonSerializer.Serialize(new
        {
            identificacion = new { ambiente = "00", tipoDte = doc.TipoDteCodigo,
                numeroControl = doc.NumeroControl, codigoGeneracion = doc.CodigoGeneracion },
            emisor = new { nit = "11111111111111" }
        });
        doc.Json!.JsonFirmado = DteFiscalIsolationTests.Jws(foreignPayload);
        await db.SaveChangesAsync();
        var query = Substitute.For<IHaciendaConsultaDteClient>();
        var auth = Substitute.For<IHaciendaAuthClient>();

        var result = await Service(db, query, Substitute.For<IHaciendaReceptionClient>(), authOverride: auth)
            .ConciliarHaciendaAsync(10, doc.Id, "auditor");

        result.ErrorCode.Should().Be("DTE_CONSULTA_INCOMPATIBLE");
        await auth.DidNotReceive().AutenticarAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await query.DidNotReceive().ConsultarAsync(Arg.Any<HaciendaConsultaDteRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ConcurrentFiscalChange_WinsAndLateQueryCannotOverwriteIt()
    {
        var options = new DbContextOptionsBuilder<NeoStpDbContext>()
            .UseInMemoryDatabase($"reconcile-race-{Guid.NewGuid():N}").Options;
        int id;
        await using (var seedDb = new NeoStpDbContext(options)) id = (await Seed(seedDb)).Id;
        await using var queryDb = new NeoStpDbContext(options);
        await using var winnerDb = new NeoStpDbContext(options);
        var query = Substitute.For<IHaciendaConsultaDteClient>();
        query.ConsultarAsync(Arg.Any<HaciendaConsultaDteRequest>(), Arg.Any<CancellationToken>())
            .Returns(async call =>
            {
                var winner = await winnerDb.DteDocumentos.SingleAsync(x => x.Id == id);
                winner.EstadoCodigo = DteEstadoCodigos.Procesado;
                winner.SelloRecibido = "SELLO-GANADOR";
                winner.ProcesadoAt = DateTime.UtcNow;
                await winnerDb.SaveChangesAsync();
                return Confirmed(call.Arg<HaciendaConsultaDteRequest>());
            });
        var webhooks = Substitute.For<IConnectWebhookDispatcher>();

        var result = await Service(queryDb, query, Substitute.For<IHaciendaReceptionClient>(), webhooks)
            .ConciliarHaciendaAsync(10, id, "late-query");

        result.ErrorCode.Should().Be("DTE_CONCURRENCY_CONFLICT");
        result.Value!.EstadoCodigo.Should().Be(DteEstadoCodigos.Procesado);
        result.Value.SelloRecibido.Should().Be("SELLO-GANADOR");
        await query.Received(1).ConsultarAsync(Arg.Any<HaciendaConsultaDteRequest>(), Arg.Any<CancellationToken>());
        await webhooks.DidNotReceive().DispatchAsync(Arg.Any<ConnectDteEventoPayload>(), Arg.Any<CancellationToken>());
        await using var verify = new NeoStpDbContext(options);
        var evidence = await verify.DteErrorOcurrencias.ToListAsync();
        evidence.Should().ContainSingle("the simulated response did confirm the same UUID");
        evidence.Single().Resuelta.Should().BeTrue();
    }

    private static async Task<DteDocumento> Seed(NeoStpDbContext db)
    {
        db.Empresas.Add(new Empresa { Id = 10, Nit = "00000000000000", RazonSocial = "SYNTHETIC" });
        db.DteConfiguracion.Add(new DteConfiguracion
        {
            EmpresaId = 10, AmbienteCodigo = DteAmbientes.Pruebas, UsuarioMh = "fixture",
            PasswordMhCifrado = "fixture", CertificadoBlob = [1]
        });
        var doc = DteFiscalIsolationTests.Documento();
        doc.EstadoCodigo = DteEstadoCodigos.Enviado;
        doc.EnviadoAt = DateTime.UtcNow.AddMinutes(-1);
        doc.GeneradoAt = doc.EnviadoAt.Value.AddMinutes(-1);
        var payload = Payload(doc);
        doc.Json = new DteDocumentoJson
        {
            JsonDte = payload, JsonFirmado = DteFiscalIsolationTests.Jws(payload),
            RespuestaHacienda = "{\"clasificaMsg\":\"TIMEOUT\"}", GeneradoAt = doc.GeneradoAt.Value
        };
        db.DteDocumentos.Add(doc);
        await db.SaveChangesAsync();
        return doc;
    }

    private static string Payload(DteDocumento doc) => JsonSerializer.Serialize(new
    {
        identificacion = new { ambiente = "00", tipoDte = doc.TipoDteCodigo,
            numeroControl = doc.NumeroControl, codigoGeneracion = doc.CodigoGeneracion },
        emisor = new { nit = "0000-000000-000-0" }
    });

    private static HaciendaConsultaDteResult Confirmed(HaciendaConsultaDteRequest req) => new()
    {
        Success = true, CodigoHttp = 200, Ambiente = req.Ambiente, Estado = "PROCESADO",
        CodigoGeneracion = req.CodigoGeneracion, SelloRecibido = "SELLO-CONSULTA", CodigoMsg = "001",
        Raw = JsonSerializer.Serialize(new { ambiente = req.Ambiente, estado = "PROCESADO",
            codigoGeneracion = req.CodigoGeneracion, selloRecibido = "SELLO-CONSULTA", codigoMsg = "001" })
    };

    private static HaciendaConsultaDteResult Mismatch(HaciendaConsultaDteRequest req, string mismatch)
    {
        var correct = Confirmed(req);
        return new()
        {
            Success = true, CodigoHttp = 200,
            Ambiente = mismatch == "ambiente" ? "01" : correct.Ambiente,
            Estado = mismatch == "estado" ? "RECHAZADO" : correct.Estado,
            CodigoGeneracion = mismatch == "uuid" ? Guid.NewGuid().ToString() : correct.CodigoGeneracion,
            SelloRecibido = mismatch == "sello" ? null : correct.SelloRecibido,
            CodigoMsg = mismatch == "codigo" ? "999" : correct.CodigoMsg,
            DescripcionMsg = "Respuesta sintética incompatible", Raw = "{\"synthetic\":true}"
        };
    }

    private static DteDocumentosService Service(NeoStpDbContext db, IHaciendaConsultaDteClient query,
        IHaciendaReceptionClient reception, IConnectWebhookDispatcher? webhooks = null,
        IHaciendaAuthClient? authOverride = null)
    {
        var auth = authOverride ?? Substitute.For<IHaciendaAuthClient>();
        if (authOverride is null)
            auth.AutenticarAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(new HaciendaAuthResult { Success = true, Token = "synthetic", ExpiresAt = DateTime.UtcNow.AddHours(1) });
        return new(db, new DteCalculator(), Substitute.For<IDteGeneratorService>(), Substitute.For<IDteSignerService>(),
            reception, Substitute.For<IHaciendaContingenciaClient>(), Substitute.For<IHaciendaEventoClient>(), auth,
            DteFiscalIsolationTests.Protector(), Substitute.For<IDtePdfService>(), Substitute.For<ITenantEmailSender>(),
            Substitute.For<IAuditoriaService>(), webhooks ?? Substitute.For<IConnectWebhookDispatcher>(), consultaDte: query);
    }

    private sealed class CaptureHandler(HttpStatusCode status, string response) : HttpMessageHandler
    {
        public string? Uri { get; private set; }
        public string? Authorization { get; private set; }
        public string? Body { get; private set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Uri = request.RequestUri!.AbsoluteUri;
            Authorization = request.Headers.Authorization?.ToString();
            Body = await request.Content!.ReadAsStringAsync(ct);
            return new HttpResponseMessage(status) { Content = new StringContent(response) };
        }
    }
}
