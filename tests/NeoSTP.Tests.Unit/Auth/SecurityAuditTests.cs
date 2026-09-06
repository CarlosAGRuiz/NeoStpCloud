using System.Text;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.CookiePolicy;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NeoSTP.Application.Auth;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Connect;
using NeoSTP.Domain.Core.Dte;
using NeoSTP.Domain.Core.Empresas;
using NeoSTP.Infrastructure.Dte;
using NeoSTP.Infrastructure.Persistence;
using NeoSTP.Infrastructure.Services;
using NeoSTP.Web.Auth;
using NSubstitute;

namespace NeoSTP.Tests.Unit.Auth;

/// <summary>
/// Characterization only: a PASS with AuditKnownDefect confirms a defect, not security acceptance.
/// Synthetic data + EF InMemory + intercept-only HTTP; no actual host, SQL, secrets or network.
/// </summary>
public sealed class SecurityAuditTests
{
    [Fact]
    public async Task RegisteredCookiePolicyPreservesOidcNonceAndCorrelationNone()
    {
        var configured = new CookiePolicyOptions();
        WebCookiePolicy.Configure(configured);
        var result = await CookieAttributes(configured.MinimumSameSitePolicy);
        result.Mode.Should().Be("form_post");
        result.Requested.Should().OnlyContain(x => x == SameSiteMode.None);
        result.Actual.Should().OnlyContain(x => x == "samesite=none");
    }

    [Fact]
    public async Task UnspecifiedPolicyPreservesOidcNonceAndCorrelationNone_Control()
    {
        var result = await CookieAttributes(SameSiteMode.Unspecified);
        result.Requested.Should().OnlyContain(x => x == SameSiteMode.None);
        result.Actual.Should().OnlyContain(x => x == "samesite=none");
    }

    [Fact]
    public async Task WebhookRejectsLoopbackWithoutDispatch()
    {
        using var db = Db();
        using var handler = new InterceptOnlyHandler();
        using var client = new HttpClient(handler);
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(Arg.Any<string>()).Returns(client);
        var hooks = new ConnectWebhookService(db, factory, NullLogger<ConnectWebhookService>.Instance);
        var hook = await hooks.CrearAsync(new CrearWebhookRequest {
            EmpresaId = 777, Url = "https://127.0.0.1:59999/synthetic-only", Eventos = ["TEST"]
        }, "synthetic-audit");
        hook.IsSuccess.Should().BeFalse();
        hook.ErrorCode.Should().Be("WEBHOOK_DESTINATION_BLOCKED");
        (await db.ConnectWebhooks.CountAsync()).Should().Be(0);
        handler.Calls.Should().Be(0);
    }

    [Fact]
    public async Task BrandingRejectsNonImageBytesAndPdfRemainsAvailable()
    {
        using var db = Db();
        var company = new Empresa { Id = 777, Nit = "00000000000000", RazonSocial = "Empresa sintética QA" };
        db.Empresas.Add(company); await db.SaveChangesAsync();
        var branding = new BrandingService(db, Substitute.For<IAuditoriaService>());
        var document = new DteDocumento { Id = 777, Empresa = company, EmpresaId = company.Id,
            TipoDteCodigo = "01", AmbienteCodigo = "PRUEBAS", EstadoCodigo = "BORRADOR", NumeroControl = "QA-NO-FISCAL",
            CodigoGeneracion = "00000000-0000-0000-0000-000000000777", FechaEmision = DateTime.UtcNow, TotalPagar = 0 };
        var renderer = new DtePdfService();
        renderer.Generar(document).Should().NotBeEmpty("the unbranded synthetic DTE is a valid rendering control");
        var result = await branding.GuardarLogoAsync(777, Encoding.UTF8.GetBytes("NOT-AN-IMAGE-SYNTHETIC"),
            "image/png", "fixture.png", "synthetic-audit");
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("VALIDATION");
        company.LogoBlob.Should().BeNull();
        var render = () => renderer.Generar(document);
        render.Should().NotThrow();
        var pdf = render();
        Encoding.ASCII.GetString(pdf, 0, 5).Should().Be("%PDF-");
    }

    private static NeoStpDbContext Db() => new(new DbContextOptionsBuilder<NeoStpDbContext>()
        .UseInMemoryDatabase("security-probe-" + Guid.NewGuid()).Options);

    private static async Task<(string Mode, SameSiteMode[] Requested, string[] Actual)> CookieAttributes(SameSiteMode minimum)
    {
        var services = new ServiceCollection().AddLogging();
        services.AddDataProtection().UseEphemeralDataProtectionProvider();
        services.AddAuthentication().AddNeoStpSso(new SsoOptions {
            Enabled = true, Google = new() { Authority = "https://accounts.google.com", ClientId = "synthetic", ClientSecret = "synthetic-not-a-secret" }
        });
        using var provider = services.BuildServiceProvider();
        var oidc = provider.GetRequiredService<IOptionsMonitor<OpenIdConnectOptions>>().Get("Google");
        var context = new DefaultHttpContext { RequestServices = provider };
        context.Request.Scheme = "https"; context.Request.Host = new HostString("security-audit.test");
        var nonce = oidc.NonceCookie.Build(context);
        var correlation = oidc.CorrelationCookie.Build(context);
        var requested = new[] { nonce.SameSite, correlation.SameSite };
        // Exact policy currently configured in Web Program.cs; avoids running its SQL seeder.
        var middleware = new CookiePolicyMiddleware(ctx => {
            ctx.Response.Cookies.Append("synthetic-nonce", "not-a-real-nonce", nonce);
            ctx.Response.Cookies.Append("synthetic-correlation", "not-a-real-correlation", correlation);
            return Task.CompletedTask;
        }, Options.Create(new CookiePolicyOptions { MinimumSameSitePolicy = minimum, Secure = CookieSecurePolicy.Always }), NullLoggerFactory.Instance);
        await middleware.Invoke(context);
        var attributes = context.Response.Headers.SetCookie.Select(h => h!.Split(';')
            .Single(p => p.Trim().StartsWith("samesite=", StringComparison.OrdinalIgnoreCase)).Trim().ToLowerInvariant()).ToArray();
        return (oidc.ResponseMode, requested, attributes);
    }

    private sealed class InterceptOnlyHandler : HttpMessageHandler
    {
        public int Calls { get; private set; }
        public bool LoopbackTarget { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Calls++; LoopbackTarget = request.RequestUri?.IsLoopback == true;
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK));
        }
    }
}
