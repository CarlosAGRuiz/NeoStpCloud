using System.Net;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NeoSTP.Infrastructure.Scan;
using Xunit;

namespace NeoSTP.Tests.Unit.Scan;

/// <summary>
/// M2.1 — GeminiScanExtractionService: extracción OCR/IA real con respuestas HTTP simuladas.
/// Verifica el mapeo del JSON del modelo y la degradación a captura manual ante fallos.
/// </summary>
public class GeminiScanExtractionServiceTests
{
    private sealed class StubHandler(Func<HttpRequestMessage, (HttpStatusCode, string)> responder) : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            LastRequest = request;
            var (code, body) = responder(request);
            return Task.FromResult(new HttpResponseMessage(code)
            {
                Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json"),
            });
        }
    }

    private sealed class StubFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private static GeminiScanExtractionService Build(Func<HttpRequestMessage, (HttpStatusCode, string)> responder, string apiKey = "test-key")
    {
        var factory = new StubFactory(new StubHandler(responder));
        var opts = Options.Create(new GeminiScanOptions { ApiKey = apiKey, Model = "gemini-2.0-flash", BaseUrl = "https://gen.example.com" });
        return new GeminiScanExtractionService(factory, opts, NullLogger<GeminiScanExtractionService>.Instance);
    }

    /// <summary>Envuelve el JSON de campos como lo haría Gemini (candidates[0].content.parts[0].text).</summary>
    private static string GeminiEnvelope(string fieldsJson)
    {
        var escaped = System.Text.Json.JsonSerializer.Serialize(fieldsJson);
        return $"{{\"candidates\":[{{\"content\":{{\"parts\":[{{\"text\":{escaped}}}]}}}}]}}";
    }

    [Fact]
    public async Task SinApiKey_NoLlamaHttp_DejaCapturaManual()
    {
        var called = false;
        var svc = Build(_ => { called = true; return (HttpStatusCode.OK, "{}"); }, apiKey: "");

        var r = await svc.ExtraerAsync(new byte[] { 1, 2, 3 }, "image/jpeg");

        r.Confianza.Should().Be(0m);
        called.Should().BeFalse();
    }

    [Fact]
    public async Task RespuestaValida_MapeaCamposYConfianza()
    {
        var fields = """
            {"emisorNombre":"Acme SA","emisorNit":"0614-1","emisorNrc":"12345","fecha":"2026-05-20",
             "tipoDocumento":"CCF","numeroControl":"DTE-0001","selloRecibido":"SELLO-9",
             "subtotal":100.00,"iva":13.00,"total":113.00,"confianza":0.92}
            """;
        var svc = Build(_ => (HttpStatusCode.OK, GeminiEnvelope(fields)));

        var r = await svc.ExtraerAsync(new byte[] { 1, 2, 3 }, "image/png");

        r.EmisorNombre.Should().Be("Acme SA");
        r.EmisorNit.Should().Be("0614-1");
        r.Fecha.Should().Be(new DateOnly(2026, 5, 20));
        r.TipoDocumento.Should().Be("CCF");
        r.NumeroControl.Should().Be("DTE-0001");
        r.Subtotal.Should().Be(100.00m);
        r.Iva.Should().Be(13.00m);
        r.Total.Should().Be(113.00m);
        r.Confianza.Should().Be(0.92m);
    }

    [Fact]
    public async Task EnviaImagenInlineYUsaModeloEnLaUrl()
    {
        HttpRequestMessage? captured = null;
        string? sent = null;
        var svc = Build(req =>
        {
            captured = req;
            sent = req.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            return (HttpStatusCode.OK, GeminiEnvelope("{\"total\":50,\"confianza\":0.5}"));
        });

        await svc.ExtraerAsync(new byte[] { 9, 9, 9 }, "application/pdf");

        captured.Should().NotBeNull();
        captured!.RequestUri!.AbsoluteUri.Should().Contain("gemini-2.0-flash:generateContent");
        captured.RequestUri.Query.Should().NotContain("key=");
        captured.Headers.GetValues("x-goog-api-key").Should().ContainSingle().Which.Should().Be("test-key");
        sent.Should().Contain("inlineData");
        sent.Should().Contain("application/pdf");
        sent.Should().Contain(Convert.ToBase64String(new byte[] { 9, 9, 9 }));
    }

    [Fact]
    public async Task ConfianzaAusente_SeDerivaDeCamposClave()
    {
        var svc = Build(_ => (HttpStatusCode.OK, GeminiEnvelope("{\"emisorNombre\":\"Acme\",\"total\":113}")));

        var r = await svc.ExtraerAsync(new byte[] { 1 }, "image/jpeg");

        r.Confianza.Should().Be(0.8m);
    }

    [Fact]
    public async Task TextoConCercaDeCodeFence_SeLimpiaYParsea()
    {
        var svc = Build(_ => (HttpStatusCode.OK, GeminiEnvelope("```json\n{\"total\":42,\"confianza\":0.7}\n```")));

        var r = await svc.ExtraerAsync(new byte[] { 1 }, "image/jpeg");

        r.Total.Should().Be(42m);
        r.Confianza.Should().Be(0.7m);
    }

    [Fact]
    public async Task HttpError_DejaCapturaManual()
    {
        var svc = Build(_ => (HttpStatusCode.TooManyRequests, "{\"error\":\"quota\"}"));

        var r = await svc.ExtraerAsync(new byte[] { 1 }, "image/jpeg");

        r.Confianza.Should().Be(0m);
    }

    [Fact]
    public async Task RespuestaSinCandidatos_DejaCapturaManual()
    {
        var svc = Build(_ => (HttpStatusCode.OK, "{\"candidates\":[]}"));

        var r = await svc.ExtraerAsync(new byte[] { 1 }, "image/jpeg");

        r.Confianza.Should().Be(0m);
    }

    [Fact]
    public async Task TextoNoJson_DejaCapturaManual()
    {
        var svc = Build(_ => (HttpStatusCode.OK, GeminiEnvelope("lo siento, no puedo leer el documento")));

        var r = await svc.ExtraerAsync(new byte[] { 1 }, "image/jpeg");

        r.Confianza.Should().Be(0m);
    }
}
