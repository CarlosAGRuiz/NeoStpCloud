using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Billing;
using NeoSTP.Application.Empresas;
using NeoSTP.Application.Licenciamiento;
using NeoSTP.Infrastructure.Billing;
using NeoSTP.Infrastructure.Persistence;
using NeoSTP.Web.Auth;
using NeoSTP.Web.Controllers;
using NSubstitute;

namespace NeoSTP.Tests.Unit.Billing;

public sealed class BillingHttpAuthorizationTests
{
    // Actual MVC routing/auth/antiforgery/controller/service and CookieCurrentUser; only
    // authentication issuance and Razor output are fixtures. No Program, SQL, seeds or network.
    private sealed class HttpFixture : IDisposable
    {
        public IHost Host { get; }
        public HttpClient Client { get; }
        public HttpFixture(BillingSecurityFixture f)
        {
            Host = new HostBuilder().ConfigureWebHost(web => web.UseTestServer().ConfigureServices(services =>
            {
                services.AddControllersWithViews(o => o.Filters.Add<FixtureViewFilter>())
                    .AddApplicationPart(typeof(BillingController).Assembly);
                services.AddDataProtection().UseEphemeralDataProtectionProvider();
                services.AddAntiforgery(o => o.HeaderName = "X-CSRF");
                services.AddHttpContextAccessor();
                services.AddScoped<ICurrentUser, CookieCurrentUser>();
                services.AddScoped<IEmpresaContext, FixtureCompany>();
                services.AddDbContext<NeoStpDbContext>(o => o.UseInMemoryDatabase(f.DatabaseName, f.Store));
                services.AddScoped<IBillingService, BillingService>();
                services.AddSingleton(f.Options);
                services.AddSingleton(f.Payments);
                services.AddSingleton(f.Email);
                services.AddSingleton(Substitute.For<IPlanesService>());
                services.AddAuthentication("SyntheticBilling")
                    .AddScheme<AuthenticationSchemeOptions, FixtureAuthentication>("SyntheticBilling", _ => { });
                services.AddAuthorization();
            }).Configure(app =>
            {
                app.UseRouting();
                app.UseAuthentication();
                app.UseAuthorization();
                app.UseEndpoints(endpoints =>
                {
                    endpoints.MapControllers();
                    endpoints.MapGet("/audit-antiforgery", async context =>
                    {
                        var tokens = context.RequestServices.GetRequiredService<IAntiforgery>().GetAndStoreTokens(context);
                        await context.Response.WriteAsJsonAsync(new { token = tokens.RequestToken });
                    });
                });
            })).Start();
            Client = Host.GetTestClient();
        }
        public async Task<HttpResponseMessage> Get(string path, string mode)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, path);
            request.Headers.Add("X-Audit-User", mode);
            return await Client.SendAsync(request);
        }
        public async Task<HttpResponseMessage> Post(string path, string mode, bool csrf = true, params (string Key, string Value)[] values)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, path)
            {
                Content = new FormUrlEncodedContent(values.Select(v => new KeyValuePair<string, string>(v.Key, v.Value))),
            };
            request.Headers.Add("X-Audit-User", mode);
            if (csrf)
            {
                using var tokenResponse = await Get("/audit-antiforgery", mode);
                using var json = JsonDocument.Parse(await tokenResponse.Content.ReadAsStringAsync());
                request.Headers.Add("X-CSRF", json.RootElement.GetProperty("token").GetString());
                request.Headers.Add("Cookie", string.Join("; ", tokenResponse.Headers.GetValues("Set-Cookie").Select(v => v.Split(';')[0])));
            }
            return await Client.SendAsync(request);
        }
        public void Dispose() { Client.Dispose(); Host.Dispose(); }
    }

    private sealed class FixtureCompany(ICurrentUser current) : IEmpresaContext
    {
        public int? CurrentEmpresaId => current.EmpresaId ?? BillingSecurityFixture.EmpresaA;
        public bool IsSupportMode => current.EmpresaId is null;
        public string? SupportEmpresaNombre => "Synthetic A";
        public void SetSupportEmpresa(int empresaId, string empresaNombre) => throw new NotSupportedException();
        public void ClearSupportEmpresa() => throw new NotSupportedException();
    }
    private sealed class FixtureAuthentication(IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger, UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var principal = Request.Headers["X-Audit-User"].ToString() switch
            {
                "central" => BillingSecurityFixture.Principal(BillingSecurityFixture.Central, "SUPERADMIN", null),
                "admin-a" => BillingSecurityFixture.Principal(BillingSecurityFixture.AdminA, "ADMIN", BillingSecurityFixture.EmpresaA),
                "admin-b" => BillingSecurityFixture.Principal(BillingSecurityFixture.AdminB, "ADMIN", BillingSecurityFixture.EmpresaB),
                "operator-a" => BillingSecurityFixture.Principal(BillingSecurityFixture.OperatorA, "OPERADOR", BillingSecurityFixture.EmpresaA),
                "tenant-super" => BillingSecurityFixture.Principal(BillingSecurityFixture.AdminA, "SUPERADMIN", BillingSecurityFixture.EmpresaA),
                "forged-platform" => BillingSecurityFixture.Principal(BillingSecurityFixture.AdminA, "SUPERADMIN", null),
                _ => null,
            };
            return Task.FromResult(principal is null ? AuthenticateResult.NoResult()
                : AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name)));
        }
    }
    private sealed class FixtureViewFilter : IAsyncResultFilter
    {
        public Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
        {
            if (context.Result is ViewResult view) context.Result = new JsonResult(view.Model);
            return next();
        }
    }

    [Theory]
    [InlineData("admin-a")]
    [InlineData("admin-b")]
    [InlineData("operator-a")]
    [InlineData("tenant-super")]
    [InlineData("forged-platform")]
    public async Task Http_TenantCannotReviewOrApproveTransfersOfEitherCompany(string mode)
    {
        await using var f = new BillingSecurityFixture();
        var a = await f.Pending(BillingSecurityFixture.EmpresaA);
        var b = await f.Pending(BillingSecurityFixture.EmpresaB);
        using var http = new HttpFixture(f);
        (await http.Get("/billing/transferencias", mode)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        foreach (var payment in new[] { a, b })
        {
            (await http.Post($"/billing/transferencias/{payment.Id}/confirmar", mode)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
            (await http.Post($"/billing/transferencias/{payment.Id}/rechazar", mode, true, ("motivo", "synthetic"))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }
        (await f.Db.BillingPayments.AsNoTracking().CountAsync(p => p.Status == "PENDIENTE_VERIFICACION")).Should().Be(2);
        (await f.Db.EmpresaPlanes.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Http_CentralCanApprove_ButAntiforgeryIsMandatory()
    {
        await using var f = new BillingSecurityFixture();
        var payment = await f.Pending(BillingSecurityFixture.EmpresaB);
        using var http = new HttpFixture(f);
        (await http.Get("/billing/transferencias", "central")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await http.Post($"/billing/transferencias/{payment.Id}/confirmar", "central", false)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await f.Db.EmpresaPlanes.CountAsync()).Should().Be(0);
        (await http.Post($"/billing/transferencias/{payment.Id}/confirmar", "central")).StatusCode.Should().Be(HttpStatusCode.Redirect);
        var persisted = await f.Db.BillingPayments.AsNoTracking().SingleAsync();
        persisted.Status.Should().Be("SUCCEEDED");
        persisted.VerificadoPor.Should().Be("audit-user-" + BillingSecurityFixture.Central);
        (await f.Db.EmpresaPlanes.SingleAsync()).EmpresaId.Should().Be(BillingSecurityFixture.EmpresaB);
    }

    [Fact]
    public async Task Http_TransferGetNeverWrites_AndProtectedPostCreatesPayment()
    {
        await using var f = new BillingSecurityFixture();
        using var http = new HttpFixture(f);
        var path = "/billing/transferencia?planId=" + BillingSecurityFixture.Basic;
        (await http.Get(path, "admin-a")).StatusCode.Should().Be(HttpStatusCode.Redirect);
        (await http.Get(path, "admin-a")).StatusCode.Should().Be(HttpStatusCode.Redirect);
        (await f.Db.BillingPayments.CountAsync()).Should().Be(0);
        (await f.Db.BillingSubscriptions.CountAsync()).Should().Be(0);
        (await http.Post("/billing/checkout/session", "admin-a", true,
            ("planId", BillingSecurityFixture.Basic.ToString()), ("metodo", "Transferencia")))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await f.Db.BillingPayments.CountAsync()).Should().Be(1);
        (await f.Db.EmpresaPlanes.CountAsync()).Should().Be(0);
    }

    [Theory]
    [InlineData("trial")]
    [InlineData("change-plan")]
    [InlineData("cancel")]
    [InlineData("checkout/session")]
    [InlineData("portal/external")]
    public async Task Http_OperatorCannotMutateOwnSubscription(string route)
    {
        await using var f = new BillingSecurityFixture();
        using var http = new HttpFixture(f);
        (await http.Post("/billing/" + route, "operator-a", true,
            ("planId", BillingSecurityFixture.Basic.ToString()), ("newPlanId", BillingSecurityFixture.Pro.ToString())))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await f.Db.BillingSubscriptions.CountAsync()).Should().Be(0);
    }
}
