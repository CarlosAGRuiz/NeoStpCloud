using System.Net;
using System.Net.Http.Json;
using System.Text.Encodings.Web;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NeoSTP.Api.Controllers;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Billing;
using NeoSTP.Application.Billing.Dtos;
using NeoSTP.Application.Common;
using NeoSTP.Domain.Core.Billing;
using NeoSTP.Infrastructure.Billing;
using NeoSTP.Infrastructure.Persistence;
using NeoSTP.Shared;
using NeoSTP.Web.Auth;

namespace NeoSTP.Tests.Unit.Billing;

// Real routing, authorization, checkout controller and BillingService; authentication issuance,
// EF InMemory and the timeout provider are synthetic. No product Program, SQL, seeds or network.
public sealed class BillingCheckoutHttpTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Anonymous_requests_are_challenged_before_checkout(bool query)
    {
        await using var f = new BillingSecurityFixture();
        using var http = new HttpFixture(f);
        using var response = query
            ? await http.Get(BillingSecurityFixture.EmpresaA, Guid.NewGuid(), null)
            : await http.Post(BillingSecurityFixture.EmpresaA, null, HttpFixture.Key);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await f.Db.BillingCheckoutIntents.CountAsync()).Should().Be(0);
        http.Provider.CheckoutCalls.Should().Be(0);
    }

    [Fact]
    public async Task Tenant_cannot_create_checkout_for_another_company()
    {
        await using var f = new BillingSecurityFixture();
        using var http = new HttpFixture(f);
        using var response = await http.Post(BillingSecurityFixture.EmpresaA, "admin-b", HttpFixture.Key);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<CheckoutSessionResult>>();
        body!.Code.Should().Be("BILLING_FORBIDDEN");
        body.Data.Should().BeNull();
        (await f.Db.BillingCheckoutIntents.CountAsync()).Should().Be(0);
        http.Provider.CheckoutCalls.Should().Be(0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Missing_or_repeated_idempotency_header_is_bad_request(bool repeated)
    {
        await using var f = new BillingSecurityFixture();
        using var http = new HttpFixture(f);
        using var response = repeated
            ? await http.Post(BillingSecurityFixture.EmpresaA, "admin-a", HttpFixture.Key, "synthetic-other-key")
            : await http.Post(BillingSecurityFixture.EmpresaA, "admin-a");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<CheckoutSessionResult>>();
        body!.Code.Should().Be(repeated ? "IDEMPOTENCY_KEY_INVALID" : "IDEMPOTENCY_KEY_REQUIRED");
        body.Data.Should().BeNull();
        (await f.Db.BillingCheckoutIntents.CountAsync()).Should().Be(0);
        http.Provider.CheckoutCalls.Should().Be(0);
    }

    [Fact]
    public async Task Disabled_checkout_returns_service_unavailable_without_provider_effect()
    {
        await using var f = new BillingSecurityFixture();
        using var http = new HttpFixture(f);
        using var response = await http.Post(BillingSecurityFixture.EmpresaA, "admin-a", HttpFixture.Key);

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<CheckoutSessionResult>>();
        body!.Code.Should().Be("BILLING_CHECKOUT_UNAVAILABLE");
        body.Data.Should().BeNull();
        (await f.Db.BillingCheckoutIntents.CountAsync()).Should().Be(0);
        http.Provider.CheckoutCalls.Should().Be(0);
    }

    [Fact]
    public async Task Ambiguous_post_preserves_reference_without_url_and_replay_never_dispatches_again()
    {
        await using var f = new BillingSecurityFixture();
        using var http = new HttpFixture(f, enabled: true);
        using var first = await http.Post(BillingSecurityFixture.EmpresaA, "admin-a", HttpFixture.Key);
        using var replay = await http.Post(BillingSecurityFixture.EmpresaA, "admin-a", HttpFixture.Key);

        first.StatusCode.Should().Be(HttpStatusCode.Conflict);
        replay.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var firstBody = await first.Content.ReadFromJsonAsync<ApiResponse<CheckoutSessionResult>>();
        var replayBody = await replay.Content.ReadFromJsonAsync<ApiResponse<CheckoutSessionResult>>();
        firstBody!.Success.Should().BeFalse();
        firstBody.Code.Should().Be("BILLING_RECONCILIATION_REQUIRED");
        firstBody.Data!.CorrelationId.Should().NotBeNull();
        firstBody.Data.CorrelationId.Should().NotBe(Guid.Empty);
        firstBody.Data.Status.Should().Be(BillingCheckoutStatuses.RequiresReconciliation);
        firstBody.Data.RedirectUrl.Should().BeNullOrEmpty();
        replayBody!.Data.Should().BeEquivalentTo(firstBody.Data);
        http.Provider.CheckoutCalls.Should().Be(1);
        (await f.Db.BillingCheckoutIntents.CountAsync()).Should().Be(1);
        (await f.Db.BillingPayments.CountAsync()).Should().Be(0);
        (await f.Db.EmpresaPlanes.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Checkout_get_is_read_only_and_filters_both_company_and_correlation()
    {
        await using var f = new BillingSecurityFixture();
        using var http = new HttpFixture(f, enabled: true);
        using var created = await http.Post(BillingSecurityFixture.EmpresaA, "admin-a", HttpFixture.Key);
        created.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var createdBody = await created.Content.ReadFromJsonAsync<ApiResponse<CheckoutSessionResult>>();
        var correlation = createdBody!.Data!.CorrelationId!.Value;
        var before = await f.Db.BillingCheckoutIntents.AsNoTracking().SingleAsync();

        for (var i = 0; i < 2; i++)
        {
            using var own = await http.Get(BillingSecurityFixture.EmpresaA, correlation, "admin-a");
            own.StatusCode.Should().Be(HttpStatusCode.OK);
            var body = await own.Content.ReadFromJsonAsync<ApiResponse<BillingCheckoutDto>>();
            body!.Data!.CorrelationId.Should().Be(correlation);
            body.Data.Status.Should().Be(BillingCheckoutStatuses.RequiresReconciliation);
            body.Data.RedirectUrl.Should().BeNull();
        }

        using var foreignCompany = await http.Get(BillingSecurityFixture.EmpresaA, correlation, "admin-b");
        using var foreignCorrelation = await http.Get(BillingSecurityFixture.EmpresaB, correlation, "admin-b");
        foreignCompany.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        foreignCorrelation.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await foreignCompany.Content.ReadFromJsonAsync<ApiResponse<BillingCheckoutDto>>())!.Data.Should().BeNull();
        (await foreignCorrelation.Content.ReadFromJsonAsync<ApiResponse<BillingCheckoutDto>>())!.Data.Should().BeNull();
        var after = await f.Db.BillingCheckoutIntents.AsNoTracking().SingleAsync();
        after.Should().BeEquivalentTo(before);
        http.Provider.CheckoutCalls.Should().Be(1);
        (await f.Db.BillingPayments.CountAsync()).Should().Be(0);
        (await f.Db.EmpresaPlanes.CountAsync()).Should().Be(0);
    }

    private sealed class HttpFixture : IDisposable
    {
        public const string Key = "synthetic-http-checkout-key";
        private readonly IHost _host;
        private readonly HttpClient _client;
        public TimeoutCheckoutProvider Provider { get; } = new();

        public HttpFixture(BillingSecurityFixture f, bool enabled = false)
        {
            var options = Options.Create(new BillingOptions
            {
                Provider = "Wompi",
                Checkout = new()
                {
                    Enabled = enabled, Provider = "Wompi", ProviderAccountId = "synthetic-account",
                    BeneficiaryId = "synthetic-beneficiary", SuccessUrl = "https://billing.example.invalid/success",
                    CancelUrl = "https://billing.example.invalid/cancel",
                },
            });
            f.Db.BillingPlanProviderMappings.Add(new()
            {
                PlanId = BillingSecurityFixture.Basic, Provider = "Wompi", ExternalPlanId = "synthetic-price",
                Currency = "USD", UnitAmount = 10, IsActive = true,
            });
            f.Db.SaveChanges();
            var resolver = new PaymentProviderResolver([Provider, new MockPaymentProvider()], options);
            _host = new HostBuilder().ConfigureWebHost(web => web.UseTestServer().ConfigureServices(services =>
            {
                services.AddControllers().AddApplicationPart(typeof(BillingCheckoutController).Assembly);
                services.AddHttpContextAccessor();
                services.AddScoped<ICurrentUser, CookieCurrentUser>();
                services.AddDbContext<NeoStpDbContext>(o => o.UseInMemoryDatabase(f.DatabaseName, f.Store));
                services.AddScoped<IBillingService, BillingService>();
                services.AddSingleton<IOptions<BillingOptions>>(options);
                services.AddSingleton<IPaymentProviderResolver>(resolver);
                services.AddSingleton(f.Email);
                services.AddAuthentication("SyntheticCheckout")
                    .AddScheme<AuthenticationSchemeOptions, FixtureAuthentication>("SyntheticCheckout", _ => { });
                services.AddAuthorization();
            }).Configure(app =>
            {
                app.UseRouting();
                app.UseAuthentication();
                app.UseAuthorization();
                app.UseEndpoints(endpoints => endpoints.MapControllers());
            })).Start();
            _client = _host.GetTestClient();
        }

        public async Task<HttpResponseMessage> Post(int empresaId, string? user, params string[] keys)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/billing/empresas/{empresaId}/checkouts")
            {
                Content = JsonContent.Create(new { PlanId = BillingSecurityFixture.Basic, Metodo = "Wompi" }),
            };
            if (user is not null) request.Headers.Add("X-Audit-User", user);
            if (keys.Length > 0) request.Headers.TryAddWithoutValidation("Idempotency-Key", keys);
            return await _client.SendAsync(request);
        }

        public async Task<HttpResponseMessage> Get(int empresaId, Guid correlationId, string? user)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/billing/empresas/{empresaId}/checkouts/{correlationId}");
            if (user is not null) request.Headers.Add("X-Audit-User", user);
            return await _client.SendAsync(request);
        }

        public void Dispose() { _client.Dispose(); _host.Dispose(); }
    }

    private sealed class FixtureAuthentication(IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger, UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var principal = Request.Headers["X-Audit-User"].ToString() switch
            {
                "admin-a" => BillingSecurityFixture.Principal(BillingSecurityFixture.AdminA, "ADMIN", BillingSecurityFixture.EmpresaA),
                "admin-b" => BillingSecurityFixture.Principal(BillingSecurityFixture.AdminB, "ADMIN", BillingSecurityFixture.EmpresaB),
                _ => null,
            };
            return Task.FromResult(principal is null ? AuthenticateResult.NoResult()
                : AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name)));
        }
    }

    private sealed class TimeoutCheckoutProvider : IPaymentProvider, IBillingCheckoutProvider
    {
        public string ProviderName => "Wompi";
        public int CheckoutCalls { get; private set; }
        public Task<Result<ProviderCheckoutSession>> CreateCheckoutAsync(ProviderCheckoutRequest request, CancellationToken ct = default)
        {
            CheckoutCalls++;
            throw new TimeoutException("Synthetic ambiguous checkout response");
        }

        public Task<Result<string>> CreateCustomerAsync(string email, int empresaId, CancellationToken ct = default)
            => throw new InvalidOperationException("Legacy customer creation must not be called.");
        public Task<Result<CheckoutSessionResult>> CreateCheckoutSessionAsync(string customerId, string externalPlanId,
            string successUrl, string cancelUrl, CancellationToken ct = default)
            => throw new InvalidOperationException("Legacy checkout must not be called.");
        public Task<Result<BillingPortalResult>> CreatePortalSessionAsync(string customerId, string returnUrl, CancellationToken ct = default)
            => throw new InvalidOperationException("Unexpected portal call.");
        public Task<Result<string>> ChangePlanAsync(string externalSubscriptionId, string newExternalPlanId, CancellationToken ct = default)
            => throw new InvalidOperationException("Unexpected plan change.");
        public Task<Result> CancelSubscriptionAsync(string externalSubscriptionId, bool atPeriodEnd, CancellationToken ct = default)
            => throw new InvalidOperationException("Unexpected cancellation.");
    }
}
