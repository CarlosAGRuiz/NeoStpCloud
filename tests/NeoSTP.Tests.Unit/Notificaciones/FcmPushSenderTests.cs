using System.Net;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NeoSTP.Application.Notificaciones;
using NeoSTP.Infrastructure.Notificaciones;
using NSubstitute;
using Xunit;

namespace NeoSTP.Tests.Unit.Notificaciones;

/// <summary>
/// M2.2 — FcmPushSender: envío por token con FCM HTTP v1 (respuestas simuladas),
/// conteo de enviados y detección de tokens inválidos/no registrados.
/// </summary>
public class FcmPushSenderTests
{
    private sealed class StubHandler(Func<HttpRequestMessage, (HttpStatusCode, string)> responder) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Calls++;
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

    private static FcmPushSender Build(Func<HttpRequestMessage, (HttpStatusCode, string)> responder, string? token = "ya29.token", string projectId = "neostp")
    {
        var factory = new StubFactory(new StubHandler(responder));
        var tokenProvider = Substitute.For<IFcmAccessTokenProvider>();
        tokenProvider.GetAccessTokenAsync(Arg.Any<CancellationToken>()).Returns(token);
        var opts = Options.Create(new FcmOptions { ProjectId = projectId, BaseUrl = "https://fcm.example.com" });
        return new FcmPushSender(factory, tokenProvider, opts, NullLogger<FcmPushSender>.Instance);
    }

    private static PushMessage Msg(params string[] tokens) => new()
    {
        Tokens = tokens,
        Titulo = "Factura vencida",
        Cuerpo = "Tienes una factura vencida",
        Data = new Dictionary<string, string> { ["tipo"] = "FacturaVencida" },
    };

    [Fact]
    public async Task SinTokens_NoLlamaFcm_Exitoso()
    {
        var svc = Build(_ => (HttpStatusCode.OK, "{}"));
        var r = await svc.EnviarAsync(Msg());
        r.Success.Should().BeTrue();
        r.Enviados.Should().Be(0);
    }

    [Fact]
    public async Task SinAccessToken_Falla()
    {
        var svc = Build(_ => (HttpStatusCode.OK, "{}"), token: null);
        var r = await svc.EnviarAsync(Msg("t1"));
        r.Success.Should().BeFalse();
        r.Detalle.Should().Contain("autenticar");
    }

    [Fact]
    public async Task SinProjectId_Falla()
    {
        var svc = Build(_ => (HttpStatusCode.OK, "{}"), projectId: "");
        var r = await svc.EnviarAsync(Msg("t1"));
        r.Success.Should().BeFalse();
    }

    [Fact]
    public async Task EnviaUnMensajePorToken_YCuentaEnviados()
    {
        var svc = Build(_ => (HttpStatusCode.OK, "{\"name\":\"projects/neostp/messages/1\"}"));
        var r = await svc.EnviarAsync(Msg("t1", "t2", "t3"));
        r.Success.Should().BeTrue();
        r.Enviados.Should().Be(3);
        r.InvalidTokens.Should().BeEmpty();
    }

    [Fact]
    public async Task UsaEndpointV1YBearer()
    {
        HttpRequestMessage? captured = null;
        var svc = Build(req => { captured = req; return (HttpStatusCode.OK, "{}"); });
        await svc.EnviarAsync(Msg("t1"));
        captured!.RequestUri!.AbsoluteUri.Should().Contain("/v1/projects/neostp/messages:send");
        captured.Headers.Authorization!.ToString().Should().Be("Bearer ya29.token");
        var body = await captured.Content!.ReadAsStringAsync();
        body.Should().Contain("\"token\":\"t1\"");
        body.Should().Contain("Factura vencida");
    }

    [Fact]
    public async Task Token404_SeReportaInvalido()
    {
        var svc = Build(req =>
        {
            var body = req.Content!.ReadAsStringAsync().Result;
            return body.Contains("\"token\":\"bad\"")
                ? (HttpStatusCode.NotFound, "{\"error\":{\"status\":\"NOT_FOUND\"}}")
                : (HttpStatusCode.OK, "{}");
        });

        var r = await svc.EnviarAsync(Msg("good", "bad"));

        r.Enviados.Should().Be(1);
        r.InvalidTokens.Should().ContainSingle().Which.Should().Be("bad");
        r.Success.Should().BeTrue();
    }

    [Fact]
    public async Task TokenUnregistered400_SeReportaInvalido()
    {
        var svc = Build(_ => (HttpStatusCode.BadRequest, "{\"error\":{\"status\":\"UNREGISTERED\"}}"));
        var r = await svc.EnviarAsync(Msg("x"));
        r.InvalidTokens.Should().ContainSingle().Which.Should().Be("x");
        // todos los tokens resultaron inválidos => se considera procesado (no reintentable)
        r.Success.Should().BeTrue();
        r.Enviados.Should().Be(0);
    }

    [Fact]
    public async Task ErrorServidor500_NoMarcaInvalido_NiCuenta()
    {
        var svc = Build(_ => (HttpStatusCode.InternalServerError, "{\"error\":{\"status\":\"INTERNAL\"}}"));
        var r = await svc.EnviarAsync(Msg("t1"));
        r.Enviados.Should().Be(0);
        r.InvalidTokens.Should().BeEmpty();
        r.Success.Should().BeFalse();
    }
}
