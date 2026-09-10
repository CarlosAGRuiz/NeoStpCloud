using System.Net;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NeoSTP.Application.Dte;
using NeoSTP.Infrastructure.Dte;
using NSubstitute;

namespace NeoSTP.Tests.Unit.Dte;

public class HaciendaEnvironmentRoutingTests
{
    [Theory]
    [InlineData("PRUEBAS", "00", "test.example.test")]
    [InlineData("PRODUCCION", "01", "prod.example.test")]
    public async Task Clientes_UsanElEndpointDelAmbienteValidado(string ambiente, string wire, string expectedHost)
    {
        var factory = Substitute.For<IHttpClientFactory>();
        using var handler = new CaptureHandler();
        factory.CreateClient(Arg.Any<string>()).Returns(_ => new HttpClient(handler, disposeHandler: false));
        var options = Options.Create(new HaciendaOptions { PruebasBaseUrl = "https://test.example.test",
            ProduccionBaseUrl = "https://prod.example.test" });
        var sourceDocument = DteFiscalIsolationTests.Documento(ambiente);
        var document = DteFiscalIsolationTests.Jws(DteFiscalIsolationTests.Payload(sourceDocument));
        var auth = await new HttpHaciendaAuthClient(factory, options, NullLogger<HttpHaciendaAuthClient>.Instance)
            .AutenticarAsync("synthetic", "synthetic", ambiente);
        auth.Success.Should().BeTrue();
        var reception = await new HttpHaciendaReceptionClient(factory, options, NullLogger<HttpHaciendaReceptionClient>.Instance)
            .EnviarAsync(new() { AmbienteCodigo = ambiente, Ambiente = wire, Documento = document, Token = "synthetic",
                Version = sourceDocument.VersionDte, TipoDte = sourceDocument.TipoDteCodigo, CodigoGeneracion = sourceDocument.CodigoGeneracion });
        reception.Success.Should().BeTrue();
        await new HttpHaciendaContingenciaClient(factory, options, NullLogger<HttpHaciendaContingenciaClient>.Instance)
            .EnviarAsync(new() { AmbienteCodigo = ambiente, Ambiente = wire, Documento = document, Token = "synthetic" });
        await new HttpHaciendaLoteClient(factory, options, NullLogger<HttpHaciendaLoteClient>.Instance)
            .EnviarLoteAsync(new() { AmbienteCodigo = ambiente, Ambiente = wire, Token = "synthetic",
                Items = [new() { Documento = document }] });
        await new HttpHaciendaConsultaLoteClient(factory, options, NullLogger<HttpHaciendaConsultaLoteClient>.Instance)
            .ConsultarLoteAsync(new() { AmbienteCodigo = ambiente, Token = "synthetic", CodigoLote = "synthetic" });
        await new HttpHaciendaConsultaDteClient(factory, options, NullLogger<HttpHaciendaConsultaDteClient>.Instance)
            .ConsultarAsync(new() { AmbienteCodigo = ambiente, Ambiente = wire, Token = "synthetic",
                NitEmisor = "00000000000000", TipoDte = "01", CodigoGeneracion = Guid.NewGuid().ToString() });
        var body = System.Text.Json.JsonSerializer.Serialize(new { ambiente = wire, documento = document });
        await new HttpHaciendaEventoClient(factory, options, NullLogger<HttpHaciendaEventoClient>.Instance)
            .PostAsync("/fesv/recepciondte", body, "synthetic", ambiente);
        handler.Requests.Should().HaveCount(7).And.OnlyContain(u => u.Host == expectedHost);
    }

    [Fact]
    public async Task Clientes_BloqueanAmbienteDesconocidoSinCrearHttpClient()
    {
        var factory = Substitute.For<IHttpClientFactory>();
        var options = Options.Create(new HaciendaOptions());
        var auth = await new HttpHaciendaAuthClient(factory, options, NullLogger<HttpHaciendaAuthClient>.Instance)
            .AutenticarAsync("synthetic", "synthetic", "otro");
        auth.Success.Should().BeFalse();
        await new HttpHaciendaContingenciaClient(factory, options, NullLogger<HttpHaciendaContingenciaClient>.Instance)
            .EnviarAsync(new() { AmbienteCodigo = "otro" });
        await new HttpHaciendaLoteClient(factory, options, NullLogger<HttpHaciendaLoteClient>.Instance)
            .EnviarLoteAsync(new() { AmbienteCodigo = "otro" });
        await new HttpHaciendaConsultaLoteClient(factory, options, NullLogger<HttpHaciendaConsultaLoteClient>.Instance)
            .ConsultarLoteAsync(new() { AmbienteCodigo = "otro" });
        await new HttpHaciendaConsultaDteClient(factory, options, NullLogger<HttpHaciendaConsultaDteClient>.Instance)
            .ConsultarAsync(new() { AmbienteCodigo = "otro" });
        await new HttpHaciendaEventoClient(factory, options, NullLogger<HttpHaciendaEventoClient>.Instance)
            .PostAsync("/fesv/recepciondte", "{}", "synthetic", "otro");
        factory.ReceivedCalls().Should().BeEmpty();
    }

    private sealed class CaptureHandler : HttpMessageHandler
    {
        public List<Uri> Requests { get; } = [];
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Requests.Add(request.RequestUri!);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(
                """{"status":"OK","body":{"token":"Bearer synthetic"},"estado":"PROCESADO","selloRecibido":"synthetic","codigoLote":"synthetic","documentos":[]}""") });
        }
    }
}
