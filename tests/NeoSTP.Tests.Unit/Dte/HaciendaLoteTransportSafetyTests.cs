using System.Net;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NeoSTP.Application.Dte;
using NeoSTP.Application.Dte.Abstractions;
using NeoSTP.Infrastructure.Dte;
using NSubstitute;
using Polly.Timeout;

namespace NeoSTP.Tests.Unit.Dte;

/// <summary>
/// Real lote/consulta client parsing and exception handling over an intercept-only primary handler.
/// No DB, host, secrets, sockets or external calls. This does not exercise the GET retry policy;
/// no-auto-retry for fiscal POSTs is covered separately by HaciendaResilienceAuditTests.
/// </summary>
public class HaciendaLoteTransportSafetyTests
{
    [Theory]
    [InlineData("network", "NETWORK_ERROR")]
    [InlineData("task-timeout", "TIMEOUT")]
    [InlineData("polly-timeout", "TIMEOUT")]
    public async Task PostFailureAfterRequestCaptureIsUncertainAndNotSuccess(string failure, string expectedCode)
    {
        using var fixture = new Fixture(failure: failure);
        var request = Lote();
        var result = await fixture.Sender.EnviarLoteAsync(request);
        result.Success.Should().BeFalse();
        result.Estado.Should().Be("ENVIADO");
        result.CodigoMsg.Should().Be(expectedCode);
        result.CodigoHttp.Should().Be(0);
        result.Raw.Should().BeNull("no response was captured; do not invent Hacienda evidence");
        fixture.Handler.Calls.Should().Be(1);
        fixture.Handler.Method.Should().Be(HttpMethod.Post);
        fixture.Handler.Path.Should().Be("/fesv/recepcionlote");
        fixture.Handler.Body.Should().Contain(request.Items[0].Documento);
        fixture.Factory.Received(1).CreateClient(HttpHaciendaLoteClient.HttpClientName);
    }

    [Theory]
    [InlineData("network", null)]
    [InlineData("task-timeout", "TIMEOUT")]
    [InlineData("polly-timeout", "TIMEOUT")]
    public async Task GetFailureCannotInventFiscalRejection(string failure, string? expectedCode)
    {
        using var fixture = new Fixture(failure: failure);
        var result = await fixture.Reader.ConsultarLoteAsync(Consulta());
        result.Success.Should().BeFalse();
        result.Estado.Should().NotBe("RECHAZADO");
        result.CodigoMsg.Should().Be(expectedCode);
        result.CodigoHttp.Should().Be(0);
        result.Items.Should().BeEmpty();
        result.Raw.Should().BeNull();
        fixture.Handler.Calls.Should().Be(1);
        fixture.Handler.Method.Should().Be(HttpMethod.Get);
        fixture.Factory.Received(1).CreateClient(HttpHaciendaConsultaLoteClient.HttpClientName);
    }

    [Theory]
    [InlineData("not-json")]
    [InlineData("[]")]
    [InlineData("null")]
    [InlineData("{\"estado\":123}")]
    [InlineData("{\"estado\":\"PROCESADO\",\"codigoLote\":{}}")]
    [InlineData("{\"estado\":\"PROCESADO\",\"observaciones\":[123]}")]
    public async Task PostInvalidResponsePreservesRawAndUncertainty(string raw)
    {
        using var fixture = new Fixture(raw);
        var result = await fixture.Sender.EnviarLoteAsync(Lote());
        result.Success.Should().BeFalse();
        result.Estado.Should().Be("ENVIADO");
        result.CodigoMsg.Should().Be("RESPUESTA_INVALIDA");
        result.CodigoHttp.Should().Be(200);
        result.Raw.Should().Be(raw);
        fixture.Handler.Calls.Should().Be(1);
    }

    [Theory]
    [InlineData("not-json")]
    [InlineData("[]")]
    [InlineData("null")]
    [InlineData("{\"estado\":123}")]
    [InlineData("{}")]
    [InlineData("{\"estado\":\"PROCESADO\",\"listadoDteEnviados\":{}}")]
    [InlineData("{\"estado\":\"\",\"listadoDteEnviados\":[]}")]
    [InlineData("{\"estado\":\"PROCESADO\",\"listadoDteEnviados\":[null]}")]
    [InlineData("{\"estado\":\"PROCESADO\",\"listadoDteEnviados\":[{\"codigoGeneracion\":123}]}")]
    public async Task GetInvalidResponsePreservesRawWithoutDocumentOutcomes(string raw)
    {
        using var fixture = new Fixture(raw);
        var result = await fixture.Reader.ConsultarLoteAsync(Consulta());
        result.Success.Should().BeFalse();
        result.CodigoMsg.Should().Be("RESPUESTA_INVALIDA");
        result.CodigoHttp.Should().Be(200);
        result.Raw.Should().Be(raw);
        result.Items.Should().BeEmpty();
        result.Estado.Should().NotBe("RECHAZADO");
    }

    [Theory]
    [InlineData("{\"estado\":\"PROCESADO\",\"selloRecibido\":\"synthetic-seal\"}")]
    [InlineData("{\"estado\":\"PROCESADO\",\"codigoLote\":null}")]
    [InlineData("{\"estado\":\"PROCESADO\",\"codigoLote\":\"   \"}")]
    public async Task ProcessedWithoutTrackingCodeIsNotSuccessfulPost(string raw)
    {
        using var fixture = new Fixture(raw);
        var result = await fixture.Sender.EnviarLoteAsync(Lote());
        result.Success.Should().BeFalse("a lote needs its tracking code before it can be consulted");
        result.Raw.Should().Be(raw);
        result.CodigoHttp.Should().Be(200);
    }

    [Fact]
    public async Task ValidPostControlCapturesCodeSealObservationsAndRaw()
    {
        const string raw = "{\"estado\":\"PROCESADO\",\"codigoLote\":\"synthetic-lote\",\"selloRecibido\":\"synthetic-seal\",\"observaciones\":[\"synthetic observation\"]}";
        using var fixture = new Fixture(raw);
        var result = await fixture.Sender.EnviarLoteAsync(Lote());
        result.Success.Should().BeTrue(); result.CodigoLote.Should().Be("synthetic-lote");
        result.SelloRecibido.Should().Be("synthetic-seal"); result.Raw.Should().Be(raw);
        result.Observaciones.Should().Equal("synthetic observation");
    }

    [Fact]
    public async Task ValidGetControlPreservesIndividualOutcomeAndRaw()
    {
        const string raw = "{\"estado\":\"PROCESADO\",\"listadoDteEnviados\":[{\"codigoGeneracion\":\"synthetic-generation\",\"estado\":\"PROCESADO\",\"selloRecibido\":\"synthetic-seal\"}]}";
        using var fixture = new Fixture(raw);
        var result = await fixture.Reader.ConsultarLoteAsync(Consulta());
        result.Success.Should().BeTrue(); result.Raw.Should().Be(raw);
        result.Items.Should().ContainSingle().Which.CodigoGeneracion.Should().Be("synthetic-generation");
        result.Items[0].Estado.Should().Be("PROCESADO"); result.Items[0].SelloRecibido.Should().Be("synthetic-seal");
    }

    [Theory]
    [InlineData(401, "NO_AUTORIZADO")]
    [InlineData(503, "ERROR")]
    public async Task NonSuccessStatusPreservesRawForBothClients(int status, string expectedState)
    {
        const string raw = "{\"codigoMsg\":\"synthetic-error\",\"descripcionMsg\":\"Synthetic failure\"}";
        using var post = new Fixture(raw, status);
        using var get = new Fixture(raw, status);
        var sent = await post.Sender.EnviarLoteAsync(Lote());
        var read = await get.Reader.ConsultarLoteAsync(Consulta());
        sent.Success.Should().BeFalse(); read.Success.Should().BeFalse();
        sent.Estado.Should().Be(expectedState); read.Estado.Should().Be(expectedState);
        sent.CodigoHttp.Should().Be(status); read.CodigoHttp.Should().Be(status);
        sent.Raw.Should().Be(raw); read.Raw.Should().Be(raw);
        sent.CodigoMsg.Should().Be("synthetic-error"); read.CodigoMsg.Should().Be("synthetic-error");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CallerCancellationIsNotConvertedToRemoteFiscalOutcome(bool query)
    {
        using var cancellation = new CancellationTokenSource();
        using var fixture = new Fixture(failure: "caller-cancel", cancelCaller: cancellation.Cancel);
        var attempt = async () => {
            if (query) await fixture.Reader.ConsultarLoteAsync(Consulta(), cancellation.Token);
            else await fixture.Sender.EnviarLoteAsync(Lote(), cancellation.Token);
        };
        await attempt.Should().ThrowAsync<OperationCanceledException>();
        fixture.Handler.Calls.Should().Be(1);
    }

    private static HaciendaLoteRequest Lote()
    {
        var document = DteFiscalIsolationTests.Documento();
        return new HaciendaLoteRequest {
            AmbienteCodigo = "PRUEBAS", Ambiente = "00", Nit = "00000000000000", SelloEvento = "synthetic-event", Token = "synthetic",
            Items = [new() { TipoDte = "01", CodigoGeneracion = document.CodigoGeneracion,
                Documento = DteFiscalIsolationTests.Jws(DteFiscalIsolationTests.Payload(document)) }]
        };
    }
    private static HaciendaConsultaLoteRequest Consulta() => new() { AmbienteCodigo = "PRUEBAS", CodigoLote = "synthetic-lote", Token = "synthetic" };

    private sealed class Fixture : IDisposable
    {
        private readonly HttpClient _client;
        internal InterceptHandler Handler { get; }
        internal IHttpClientFactory Factory { get; }
        internal HttpHaciendaLoteClient Sender { get; }
        internal HttpHaciendaConsultaLoteClient Reader { get; }
        internal Fixture(string raw = "{}", int status = 200, string? failure = null, Action? cancelCaller = null)
        {
            Handler = new InterceptHandler(raw, status, failure, cancelCaller);
            _client = new HttpClient(Handler);
            Factory = Substitute.For<IHttpClientFactory>(); Factory.CreateClient(Arg.Any<string>()).Returns(_client);
            var options = Options.Create(new HaciendaOptions { PruebasBaseUrl = "https://synthetic.invalid", ProduccionBaseUrl = "https://synthetic.invalid", TimeoutSeconds = 5 });
            Sender = new HttpHaciendaLoteClient(Factory, options, NullLogger<HttpHaciendaLoteClient>.Instance);
            Reader = new HttpHaciendaConsultaLoteClient(Factory, options, NullLogger<HttpHaciendaConsultaLoteClient>.Instance);
        }
        public void Dispose() => _client.Dispose();
    }

    private sealed class InterceptHandler(string raw, int status, string? failure, Action? cancelCaller) : HttpMessageHandler
    {
        internal int Calls { get; private set; }
        internal HttpMethod? Method { get; private set; }
        internal string? Path { get; private set; }
        internal string? Body { get; private set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Calls++; Method = request.Method; Path = request.RequestUri?.AbsolutePath;
            Body = request.Content is null ? null : await request.Content.ReadAsStringAsync(ct);
            if (failure == "network") throw new HttpRequestException("Synthetic response lost after capture");
            if (failure == "task-timeout") throw new TaskCanceledException("Synthetic timeout, caller not canceled");
            if (failure == "polly-timeout") throw new TimeoutRejectedException("Synthetic policy timeout");
            if (failure == "caller-cancel") { cancelCaller!(); throw new OperationCanceledException(ct); }
            return new HttpResponseMessage((HttpStatusCode)status) { Content = new StringContent(raw) };
        }
    }
}
