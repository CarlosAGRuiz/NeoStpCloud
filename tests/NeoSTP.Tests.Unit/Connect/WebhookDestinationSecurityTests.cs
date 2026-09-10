using System.Net;
using System.Net.Sockets;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NeoSTP.Application.Connect;
using NeoSTP.Domain.Core.Connect;
using NeoSTP.Infrastructure;
using NeoSTP.Infrastructure.Connect;
using NeoSTP.Infrastructure.Persistence;
using NeoSTP.Infrastructure.Services;
using NSubstitute;

namespace NeoSTP.Tests.Unit.Connect;

/// <summary>SEC01 acceptance. All DNS/socket/HTTP endpoints are intercepted; no host, SQL or network.</summary>
public class WebhookDestinationSecurityTests
{
    [Theory]
    [InlineData("http://public.example/hook")]
    [InlineData("file:///etc/hosts")]
    [InlineData("https://user:password@public.example/hook")]
    [InlineData("https://public.example/hook#fragment")]
    [InlineData("https://localhost/hook")]
    [InlineData("https://child.localhost./hook")]
    [InlineData("https://metadata.google.internal/hook")]
    [InlineData("https://server.local/hook")]
    [InlineData("https://singlelabel/hook")]
    [InlineData("https://127.1/hook")]
    [InlineData("https://2130706433/hook")]
    [InlineData("https://0x7f000001/hook")]
    [InlineData("https://0177.0.0.1/hook")]
    [InlineData("https://[::ffff:127.0.0.1]/hook")]
    [InlineData("https://[fe80::1%251]/hook")]
    [InlineData("https://public.example\\@127.0.0.1/hook")]
    public void UnsafeUrlsAreRejectedBeforeDns(string url)
        => ((Action)(() => WebhookDestinationPolicy.ValidateUrl(url))).Should().Throw<WebhookDestinationException>();

    [Theory]
    [InlineData("0.0.0.1")]
    [InlineData("10.255.255.255")]
    [InlineData("100.64.0.1")]
    [InlineData("100.127.255.255")]
    [InlineData("127.255.255.254")]
    [InlineData("169.254.169.254")]
    [InlineData("172.16.0.1")]
    [InlineData("172.31.255.255")]
    [InlineData("192.168.1.1")]
    [InlineData("192.0.0.1")]
    [InlineData("192.0.2.1")]
    [InlineData("192.88.99.1")]
    [InlineData("198.18.0.1")]
    [InlineData("198.19.255.255")]
    [InlineData("198.51.100.1")]
    [InlineData("203.0.113.1")]
    [InlineData("224.0.0.1")]
    [InlineData("239.255.255.255")]
    [InlineData("240.0.0.1")]
    [InlineData("255.255.255.255")]
    [InlineData("::")]
    [InlineData("::1")]
    [InlineData("::ffff:10.0.0.1")]
    [InlineData("64:ff9b::a00:1")]
    [InlineData("100::1")]
    [InlineData("2001::a00:1")]
    [InlineData("2001:db8::1")]
    [InlineData("2002:7f00:1::1")]
    [InlineData("3fff::1")]
    [InlineData("fc00::1")]
    [InlineData("fdff::1")]
    [InlineData("fe80::1")]
    [InlineData("fec0::1")]
    [InlineData("ff02::1")]
    [InlineData("4000::1")]
    [InlineData("2606:4700::1111%1")]
    public void PrivateSpecialAndReservedAddressesAreRejected(string ip)
        => WebhookDestinationPolicy.IsPublicAddress(IPAddress.Parse(ip)).Should().BeFalse();

    [Theory]
    [InlineData("8.8.8.8")]
    [InlineData("1.1.1.1")]
    [InlineData("100.63.255.255")]
    [InlineData("100.128.0.1")]
    [InlineData("172.15.255.255")]
    [InlineData("172.32.0.1")]
    [InlineData("2606:4700:4700::1111")]
    [InlineData("::ffff:8.8.8.8")]
    public void PublicAddressControlsAreAccepted(string ip)
        => WebhookDestinationPolicy.IsPublicAddress(IPAddress.Parse(ip)).Should().BeTrue();

    [Theory]
    [InlineData("10.0.0.1")]
    [InlineData("::1")]
    [InlineData("::ffff:169.254.169.254")]
    public async Task AnyPrivateDnsAnswerBlocksEntireConnectionWithoutSocket(string privateIp)
    {
        var connects = 0;
        var policy = Policy("8.8.8.8", privateIp);
        using var request = Request();
        var attempt = async () => await WebhookHttpTransport.ConnectAsync(policy, request, Endpoint(),
            (_, _) => { connects++; return ValueTask.FromResult<Stream>(new MemoryStream()); }, default);
        await attempt.Should().ThrowAsync<WebhookDestinationException>();
        connects.Should().Be(0);
    }

    [Fact]
    public async Task EmptyDnsFailsClosedWithoutSocket()
    {
        var policy = Policy();
        var attempt = () => policy.ResolvePublicAsync(new Uri("https://public.example/hook"), default);
        await attempt.Should().ThrowAsync<WebhookDestinationException>();
    }

    [Fact]
    public async Task RegistrationRejectsPrivateDnsAndPersistsNothing()
    {
        using var db = Db();
        var factory = Substitute.For<IHttpClientFactory>();
        var service = new ConnectWebhookService(db, factory, NullLogger<ConnectWebhookService>.Instance, Policy("10.0.0.1"));
        var result = await service.CrearAsync(new CrearWebhookRequest { EmpresaId = 7, Url = "https://public.example/hook", Eventos = ["TEST"] }, "audit");
        result.ErrorCode.Should().Be("WEBHOOK_DESTINATION_BLOCKED");
        (await db.ConnectWebhooks.CountAsync()).Should().Be(0);
        factory.DidNotReceive().CreateClient(Arg.Any<string>());
    }

    [Fact]
    public async Task RegistrationPublicControlPreservesTenant()
    {
        using var db = Db();
        var service = new ConnectWebhookService(db, Substitute.For<IHttpClientFactory>(), NullLogger<ConnectWebhookService>.Instance, Policy("8.8.8.8"));
        var result = await service.CrearAsync(new CrearWebhookRequest { EmpresaId = 7, Url = "https://public.example/hook", Eventos = ["TEST"] }, "audit");
        result.IsSuccess.Should().BeTrue();
        result.Value!.EmpresaId.Should().Be(7);
        (await service.ObtenerAsync(result.Value.Id, 8)).ErrorCode.Should().Be("WEBHOOK_NOT_FOUND");
        (await service.TestAsync(result.Value.Id, 8, "audit")).ErrorCode.Should().Be("WEBHOOK_NOT_FOUND");
    }

    [Fact]
    public async Task DnsRebindingIsRecheckedAtConnectionAndNeverReResolvedBySocket()
    {
        var queries = 0;
        var policy = new WebhookDestinationPolicy((_, _) => Task.FromResult(new[] {
            IPAddress.Parse(++queries == 1 ? "8.8.8.8" : "127.0.0.1") }));
        using var request = Request();
        var endpoints = new List<IPEndPoint>();
        ValueTask<Stream> Connect(IPEndPoint endpoint, CancellationToken _) {
            endpoints.Add(endpoint); return ValueTask.FromResult<Stream>(new MemoryStream());
        }
        using var stream = await WebhookHttpTransport.ConnectAsync(policy, request, Endpoint(), Connect, default);
        queries.Should().Be(1);
        endpoints.Should().ContainSingle().Which.Address.Should().Be(IPAddress.Parse("8.8.8.8"));
        request.RequestUri!.Host.Should().Be("public.example", "the original hostname remains available for SNI/certificate checks");
        request.Headers.Host.Should().BeNull();
        var reconnect = async () => await WebhookHttpTransport.ConnectAsync(policy, request, Endpoint(), Connect, default);
        await reconnect.Should().ThrowAsync<WebhookDestinationException>();
        queries.Should().Be(2);
        endpoints.Should().ContainSingle("a rebound private IP is never handed to the connector");
    }

    [Fact]
    public async Task RegistrationDnsSnapshotCannotAuthorizeLaterPrivateConnection()
    {
        var queries = 0;
        var policy = new WebhookDestinationPolicy((_, _) => Task.FromResult(new[] { IPAddress.Parse(++queries == 1 ? "8.8.8.8" : "10.0.0.1") }));
        await policy.ResolvePublicAsync(new Uri("https://public.example/hook"), default);
        using var request = Request();
        var connects = 0;
        var attempt = async () => await WebhookHttpTransport.ConnectAsync(policy, request, Endpoint(),
            (_, _) => { connects++; return ValueTask.FromResult<Stream>(new MemoryStream()); }, default);
        await attempt.Should().ThrowAsync<WebhookDestinationException>();
        queries.Should().Be(2); connects.Should().Be(0);
    }

    [Fact]
    public async Task ConnectionFallbackUsesOnlyValidatedNumericEndpoints()
    {
        using var request = Request();
        var endpoints = new List<IPEndPoint>();
        using var stream = await WebhookHttpTransport.ConnectAsync(Policy("8.8.8.8", "1.1.1.1"), request, Endpoint(),
            (endpoint, _) => {
                endpoints.Add(endpoint);
                return endpoints.Count == 1 ? ValueTask.FromException<Stream>(new SocketException((int)SocketError.ConnectionRefused))
                    : ValueTask.FromResult<Stream>(new MemoryStream());
            }, default);
        endpoints.Select(e => e.Address.ToString()).Should().Equal("8.8.8.8", "1.1.1.1");
        endpoints.Should().OnlyContain(e => e.Port == 443);
    }

    [Fact]
    public async Task RealSocketsHandlerCallbackUsesInjectedSocketAndKeepsTlsDefaults()
    {
        var queries = 0;
        var policy = new WebhookDestinationPolicy((_, _) => { queries++; return Task.FromResult(new[] { IPAddress.Parse("8.8.8.8") }); });
        var endpoints = new List<IPEndPoint>();
        using var handler = WebhookHttpTransport.CreateHandler(policy, (endpoint, _) => {
            endpoints.Add(endpoint); throw new InvalidOperationException("Synthetic stop before TLS; no socket");
        });
        handler.AllowAutoRedirect.Should().BeFalse(); handler.UseProxy.Should().BeFalse(); handler.UseCookies.Should().BeFalse();
        handler.SslOptions.RemoteCertificateValidationCallback.Should().BeNull("normal TLS chain/name validation must not be overridden");
        handler.SslOptions.TargetHost.Should().BeNull("SocketsHttpHandler derives SNI from the original request URI");
        using var client = new HttpClient(handler);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        using var request = Request();
        var attempt = () => client.SendAsync(request, deadline.Token);
        await attempt.Should().ThrowAsync<HttpRequestException>();
        queries.Should().Be(1); endpoints.Should().ContainSingle().Which.Address.Should().Be(IPAddress.Parse("8.8.8.8"));
    }

    [Theory]
    [InlineData("http://public.example/hook", "public.example", 80, false)]
    [InlineData("https://public.example/hook", "other.example", 443, false)]
    [InlineData("https://public.example/hook", "public.example", 444, false)]
    [InlineData("https://public.example/hook", "public.example", 443, true)]
    public async Task CallbackRejectsUnexpectedAuthorityOrHostOverride(string url, string host, int port, bool overrideHost)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        if (overrideHost) request.Headers.Host = "other.example";
        var calls = 0;
        var attempt = async () => await WebhookHttpTransport.ConnectAsync(Policy("8.8.8.8"), request, new DnsEndPoint(host, port),
            (_, _) => { calls++; return ValueTask.FromResult<Stream>(new MemoryStream()); }, default);
        await attempt.Should().ThrowAsync<WebhookDestinationException>();
        calls.Should().Be(0);
    }

    [Theory]
    [InlineData("http://public.example/hook")]
    [InlineData("https://127.0.0.1/hook")]
    [InlineData("https://[::1]/hook")]
    [InlineData("https://user:secret@public.example/hook")]
    public async Task LegacyRowsAreBlockedForTestAndWorkerWithoutHttp(string url)
    {
        using var db = Db();
        await Seed(db, url);
        var factory = Substitute.For<IHttpClientFactory>();
        var service = new ConnectWebhookService(db, factory, NullLogger<ConnectWebhookService>.Instance);
        var result = await service.TestAsync(1, 7, "audit");
        result.IsFailure.Should().BeTrue();
        var worker = new ConnectWebhookDispatcher(db, factory, NullLogger<ConnectWebhookDispatcher>.Instance);
        await worker.ProcesarPendientesAsync();
        var deliveries = await db.ConnectWebhookDeliveries.AsNoTracking().ToListAsync();
        deliveries.Should().HaveCount(2).And.OnlyContain(d => d.Estado == ConnectDeliveryEstados.Fallido
            && d.HttpStatus == null && d.Error == WebhookDestinationPolicy.BlockedMessage);
        factory.DidNotReceive().CreateClient(Arg.Any<string>());
    }

    [Fact]
    public async Task RedirectResponsesAreNotSuccessAndDoNotTriggerAnotherRequest()
    {
        using var db = Db(); await Seed(db, "https://public.example/hook");
        using var handler = new RedirectOnlyHandler();
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(WebhookHttpTransport.HttpClientName).Returns(_ => new HttpClient(handler, disposeHandler: false));
        var service = new ConnectWebhookService(db, factory, NullLogger<ConnectWebhookService>.Instance);
        var result = await service.TestAsync(1, 7, "audit");
        var worker = new ConnectWebhookDispatcher(db, factory, NullLogger<ConnectWebhookDispatcher>.Instance);
        await worker.ProcesarPendientesAsync();
        result.IsFailure.Should().BeTrue();
        handler.Calls.Should().Be(2, "one test and one worker attempt, with no redirect-following send in business code");
        (await db.ConnectWebhookDeliveries.ToListAsync()).Should().OnlyContain(d => d.HttpStatus == 302
            && d.Estado != ConnectDeliveryEstados.Entregado && d.Error!.Contains("redirección"));
        factory.Received(2).CreateClient(WebhookHttpTransport.HttpClientName);
    }

    [Fact]
    public void ActualInfrastructureRegistrationHasDedicatedSafePrimaryHandler()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
            ["ConnectionStrings:NeoStpDb"] = "Server=synthetic.invalid;Database=NeverOpened;Integrated Security=true;TrustServerCertificate=true",
        }).Build();
        var services = new ServiceCollection().AddLogging(); services.AddInfrastructure(config);
        using var provider = services.BuildServiceProvider();
        using var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient(WebhookHttpTransport.HttpClientName);
        client.Timeout.Should().Be(TimeSpan.FromSeconds(10));
        client.DefaultRequestVersion.Should().Be(HttpVersion.Version11);
        client.DefaultVersionPolicy.Should().Be(HttpVersionPolicy.RequestVersionExact);
        var chain = provider.GetRequiredService<IHttpMessageHandlerFactory>().CreateHandler(WebhookHttpTransport.HttpClientName);
        while (chain is DelegatingHandler delegating) chain = delegating.InnerHandler!;
        var sockets = chain.Should().BeOfType<SocketsHttpHandler>().Subject;
        sockets.ConnectCallback.Should().NotBeNull(); sockets.AllowAutoRedirect.Should().BeFalse(); sockets.UseProxy.Should().BeFalse();
        sockets.SslOptions.RemoteCertificateValidationCallback.Should().BeNull();
        // Deliberately no SendAsync: this verifies the real DI graph without allowing any real socket.
    }

    private static WebhookDestinationPolicy Policy(params string[] ips)
        => new((_, _) => Task.FromResult(ips.Select(IPAddress.Parse).ToArray()));
    private static HttpRequestMessage Request() => new(HttpMethod.Post, "https://public.example/hook") {
        Version = HttpVersion.Version11, VersionPolicy = HttpVersionPolicy.RequestVersionExact };
    private static DnsEndPoint Endpoint() => new("public.example", 443);
    private static NeoStpDbContext Db() => new(new DbContextOptionsBuilder<NeoStpDbContext>().UseInMemoryDatabase("ssrf-" + Guid.NewGuid()).Options);
    private static async Task Seed(NeoStpDbContext db, string url)
    {
        db.ConnectWebhooks.Add(new ConnectWebhook { Id = 1, EmpresaId = 7, Url = url, SecretoHmac = "synthetic", Eventos = "TEST", Activo = true });
        db.ConnectWebhookDeliveries.Add(new ConnectWebhookDelivery { Id = 10, WebhookId = 1, EmpresaId = 7, Evento = "TEST", Payload = "{}",
            Estado = ConnectDeliveryEstados.Pendiente, ProximoIntento = DateTime.UtcNow.AddMinutes(-1) });
        await db.SaveChangesAsync();
    }
    private sealed class RedirectOnlyHandler : HttpMessageHandler
    {
        public int Calls { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Calls++;
            var response = new HttpResponseMessage(HttpStatusCode.Redirect);
            response.Headers.Location = new Uri("https://127.0.0.1/never-contacted");
            return Task.FromResult(response);
        }
    }
}
