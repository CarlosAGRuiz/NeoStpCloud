using System.Net;
using System.Security.Cryptography;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NeoSTP.Infrastructure.Notificaciones;
using Xunit;

namespace NeoSTP.Tests.Unit.Notificaciones;

/// <summary>
/// M2.2 — ServiceAccountTokenProvider: firma un JWT RS256 con la clave privada del service
/// account y lo canjea por un access token, con caché. HTTP simulado y clave RSA generada.
/// </summary>
public class ServiceAccountTokenProviderTests
{
    private sealed class StubHandler(Func<HttpRequestMessage, (HttpStatusCode, string)> responder) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        public string? LastBody { get; private set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Calls++;
            LastBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(ct);
            var (code, body) = responder(request);
            return new HttpResponseMessage(code) { Content = new StringContent(body) };
        }
    }

    private sealed class StubFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private static string NewPrivateKeyPem()
    {
        using var rsa = RSA.Create(2048);
        return rsa.ExportPkcs8PrivateKeyPem();
    }

    private static (ServiceAccountTokenProvider svc, StubHandler handler) Build(
        Func<HttpRequestMessage, (HttpStatusCode, string)> responder, string? privateKey = null)
    {
        var handler = new StubHandler(responder);
        var factory = new StubFactory(handler);
        var opts = Options.Create(new FcmOptions
        {
            ProjectId = "neostp",
            ClientEmail = "svc@neostp.iam.gserviceaccount.com",
            PrivateKey = privateKey ?? NewPrivateKeyPem(),
            TokenUri = "https://oauth2.example.com/token",
        });
        return (new ServiceAccountTokenProvider(factory, opts, NullLogger<ServiceAccountTokenProvider>.Instance), handler);
    }

    [Fact]
    public async Task FirmaYCanjea_DevuelveAccessToken_YEnviaJwtBearer()
    {
        var (svc, handler) = Build(_ => (HttpStatusCode.OK, "{\"access_token\":\"ya29.abc\",\"expires_in\":3600}"));

        var token = await svc.GetAccessTokenAsync();

        token.Should().Be("ya29.abc");
        handler.LastBody.Should().Contain("grant_type=urn");
        handler.LastBody.Should().Contain("assertion=");
    }

    [Fact]
    public async Task Cachea_NoVuelveALlamarMientrasNoExpire()
    {
        var (svc, handler) = Build(_ => (HttpStatusCode.OK, "{\"access_token\":\"ya29.cache\",\"expires_in\":3600}"));

        var t1 = await svc.GetAccessTokenAsync();
        var t2 = await svc.GetAccessTokenAsync();

        t1.Should().Be("ya29.cache");
        t2.Should().Be("ya29.cache");
        handler.Calls.Should().Be(1);
    }

    [Fact]
    public async Task SinCredenciales_DevuelveNull_SinLlamar()
    {
        var (svc, handler) = Build(_ => (HttpStatusCode.OK, "{}"), privateKey: "");

        var token = await svc.GetAccessTokenAsync();

        token.Should().BeNull();
        handler.Calls.Should().Be(0);
    }

    [Fact]
    public async Task ErrorHttp_DevuelveNull()
    {
        var (svc, _) = Build(_ => (HttpStatusCode.Unauthorized, "{\"error\":\"invalid_grant\"}"));

        var token = await svc.GetAccessTokenAsync();

        token.Should().BeNull();
    }
}
