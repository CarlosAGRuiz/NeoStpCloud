using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NeoSTP.Application.Dte;
using NeoSTP.Application.Dte.Abstractions;
using NeoSTP.Infrastructure.Dte;
using NSubstitute;

namespace NeoSTP.Tests.Unit.Dte;

public class HaciendaReceptionIdentityTests
{
    [Theory]
    [InlineData("version")]
    [InlineData("type")]
    [InlineData("generation")]
    [InlineData("environment")]
    [InlineData("wire-environment")]
    [InlineData("zero")]
    [InlineData("missing-generation")]
    [InlineData("malformed-jws")]
    [InlineData("duplicate-version")]
    [InlineData("duplicate-identity")]
    public async Task IncoherentEnvelopeNeverCreatesHttpClientOrSends(string mismatch)
    {
        var factory = Substitute.For<IHttpClientFactory>();
        var document = DteFiscalIsolationTests.Documento(); document.VersionDte = 2;
        var payload = DteFiscalIsolationTests.Payload(document);
        var request = new HaciendaReceptionRequest { Version = 2, TipoDte = document.TipoDteCodigo,
            CodigoGeneracion = document.CodigoGeneracion, Documento = DteFiscalIsolationTests.Jws(payload), Token = "synthetic" };
        switch (mismatch)
        {
            case "version": request.Version = 1; break;
            case "type": request.TipoDte = "03"; break;
            case "generation": request.CodigoGeneracion = Guid.NewGuid().ToString(); break;
            case "environment": request.AmbienteCodigo = "PRODUCCION"; request.Ambiente = "01"; break;
            case "wire-environment": request.Ambiente = "01"; break;
            case "zero": request.Version = 0; break;
            case "missing-generation": request.CodigoGeneracion = ""; break;
            case "malformed-jws": request.Documento = "not-a-jws"; break;
            case "duplicate-version": request.Documento = DteFiscalIsolationTests.Jws(payload.Replace("\"version\":2", "\"version\":1,\"version\":2")); break;
            default: request.Documento = DteFiscalIsolationTests.Jws("{\"identificacion\":{}," + payload[1..]); break;
        }
        var result = await new HttpHaciendaReceptionClient(factory, Options.Create(new HaciendaOptions()),
            NullLogger<HttpHaciendaReceptionClient>.Instance).EnviarAsync(request);
        result.Success.Should().BeFalse(); result.CodigoMsg.Should().Be("DTE_PAYLOAD_INCOMPATIBLE");
        factory.ReceivedCalls().Should().BeEmpty();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(4)]
    public async Task MatchingVersionAndIdentityAreForwardedUnchangedOnce(int version)
    {
        var document = DteFiscalIsolationTests.Documento(); document.VersionDte = version;
        using var handler = new Capture();
        var factory = Substitute.For<IHttpClientFactory>(); factory.CreateClient(Arg.Any<string>()).Returns(_ => new HttpClient(handler, false));
        var request = new HaciendaReceptionRequest { Version = version, TipoDte = document.TipoDteCodigo,
            CodigoGeneracion = document.CodigoGeneracion, Documento = DteFiscalIsolationTests.Jws(DteFiscalIsolationTests.Payload(document)), Token = "synthetic" };
        var result = await new HttpHaciendaReceptionClient(factory, Options.Create(new HaciendaOptions
            { PruebasBaseUrl = "https://synthetic.invalid" }), NullLogger<HttpHaciendaReceptionClient>.Instance).EnviarAsync(request);
        result.Success.Should().BeTrue(); handler.Calls.Should().Be(1);
        using var body = JsonDocument.Parse(handler.Body!);
        body.RootElement.GetProperty("version").GetInt32().Should().Be(version);
        body.RootElement.GetProperty("tipoDte").GetString().Should().Be(document.TipoDteCodigo);
        body.RootElement.GetProperty("codigoGeneracion").GetString().Should().Be(document.CodigoGeneracion);
        body.RootElement.GetProperty("documento").GetString().Should().Be(request.Documento);
    }

    private sealed class Capture : HttpMessageHandler
    {
        internal int Calls { get; private set; }
        internal string? Body { get; private set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Calls++; Body = await request.Content!.ReadAsStringAsync(ct);
            return new(HttpStatusCode.OK) { Content = new StringContent("{\"estado\":\"PROCESADO\",\"selloRecibido\":\"synthetic\"}") };
        }
    }
}
