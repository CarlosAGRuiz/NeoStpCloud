using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NeoSTP.Application.Billing;
using NeoSTP.Application.Billing.Dtos;
using NeoSTP.Application.Common;
using NeoSTP.Domain.Core.Billing;
using NeoSTP.Infrastructure.Billing;
using NeoSTP.Infrastructure.Persistence;

namespace NeoSTP.Tests.Unit.Billing;

/// <summary>InMemory + synthetic provider only. No host, SQL, credentials or network.</summary>
public sealed class BillingCheckoutIntentTests
{
    [Fact]
    public async Task Checkout_persists_commercial_snapshot_and_lease_before_provider_without_granting_payment_or_license()
    {
        await using var f = new Fixture();
        f.Provider.OnCheckout = async request =>
        {
            await using var observer = f.Observe();
            var intent = await observer.Set<BillingCheckoutIntent>().SingleAsync();
            intent.CorrelationId.Should().Be(request.CorrelationId).And.NotBeEmpty();
            intent.Status.Should().Be(BillingCheckoutStatuses.Processing);
            intent.LeaseId.Should().NotBeNullOrWhiteSpace();
            intent.LeaseExpiresAt.Should().BeAfter(DateTime.UtcNow);
            intent.EmpresaId.Should().Be(BillingSecurityFixture.EmpresaA);
            intent.PlanId.Should().Be(BillingSecurityFixture.Basic);
            intent.PlanCode.Should().Be("AUDIT_BASIC");
            intent.PlanName.Should().Be("Basic");
            intent.Amount.Should().Be(10m);
            intent.Currency.Should().Be("USD");
            intent.Provider.Should().Be("Wompi");
            intent.ProviderAccountId.Should().Be(Fixture.Account);
            intent.BeneficiaryId.Should().Be("synthetic-beneficiary");
            intent.ExternalPlanId.Should().Be("synthetic-price-basic");
            intent.SuccessUrl.Should().Be(Fixture.Success);
            intent.CancelUrl.Should().Be(Fixture.Cancel);
            intent.IdempotencyKeyHash.Should().NotBeNullOrWhiteSpace().And.NotBe(Fixture.Key);
            intent.RequestFingerprint.Should().NotBeNullOrWhiteSpace();
            request.Amount.Should().Be(intent.Amount);
            request.Currency.Should().Be(intent.Currency);
            request.BillingInterval.Should().Be("MONTH");
            observer.BillingPayments.Should().BeEmpty();
            observer.EmpresaPlanes.Should().BeEmpty();
            return FakeProvider.Accept(request);
        };

        var result = await f.Service.CreateCheckoutSessionAsync(f.Request());

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.CorrelationId.Should().NotBeNull();
        result.Value.Status.Should().Be(BillingCheckoutStatuses.AwaitingPayment);
        var saved = await f.Db.Set<BillingCheckoutIntent>().SingleAsync();
        saved.Status.Should().Be(BillingCheckoutStatuses.AwaitingPayment);
        saved.ExternalCheckoutId.Should().Be("synthetic-session");
        saved.ProviderAcknowledgedAt.Should().NotBeNull();
        saved.CompletedAt.Should().BeNull();
        f.Db.BillingPayments.Should().BeEmpty();
        f.Db.EmpresaPlanes.Should().BeEmpty();
        f.Provider.CheckoutCalls.Should().Be(1);
        f.Provider.LegacyCheckoutCalls.Should().Be(0);
    }

    [Fact]
    public async Task Replay_keeps_original_snapshot_after_plan_and_mapping_price_changes()
    {
        await using var f = new Fixture();
        var first = await f.Service.CreateCheckoutSessionAsync(f.Request());
        first.IsSuccess.Should().BeTrue(first.Error);
        var plan = await f.Db.Planes.FindAsync(BillingSecurityFixture.Basic);
        plan!.PrecioMensual = 99;
        plan.Nombre = "Renamed plan";
        (await f.Db.BillingPlanProviderMappings.SingleAsync()).UnitAmount = 99;
        await f.Db.SaveChangesAsync();

        var replay = await f.Service.CreateCheckoutSessionAsync(f.Request());

        replay.IsSuccess.Should().BeTrue(replay.Error);
        replay.Value.Should().BeEquivalentTo(first.Value);
        f.Provider.CheckoutCalls.Should().Be(1);
        var intent = await f.Db.Set<BillingCheckoutIntent>().SingleAsync();
        intent.Amount.Should().Be(10);
        intent.PlanName.Should().Be("Basic");
    }

    [Fact]
    public async Task Same_key_with_different_request_conflicts_without_second_provider_call()
    {
        await using var f = new Fixture();
        (await f.Service.CreateCheckoutSessionAsync(f.Request())).IsSuccess.Should().BeTrue();

        var result = await f.Service.CreateCheckoutSessionAsync(f.Request() with { PlanId = BillingSecurityFixture.Pro });

        result.ErrorCode.Should().Be("IDEMPOTENCY_CONFLICT");
        f.Provider.CheckoutCalls.Should().Be(1);
        f.Db.Set<BillingCheckoutIntent>().Should().ContainSingle();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Missing_key_is_rejected_before_any_provider_effect(string? key)
    {
        await using var f = new Fixture();
        var result = await f.Service.CreateCheckoutSessionAsync(f.Request() with { IdempotencyKey = key });
        result.ErrorCode.Should().Be("IDEMPOTENCY_KEY_REQUIRED");
        f.AssertNoEffect();
    }

    [Fact]
    public async Task Invalid_key_is_rejected_before_any_provider_effect()
    {
        await using var f = new Fixture();
        var result = await f.Service.CreateCheckoutSessionAsync(f.Request() with { IdempotencyKey = new string('x', 1024) });
        result.ErrorCode.Should().Be("IDEMPOTENCY_KEY_INVALID");
        f.AssertNoEffect();
    }

    [Theory]
    [InlineData("zero")]
    [InlineData("negative")]
    [InlineData("fractional-cent")]
    [InlineData("inactive-plan")]
    [InlineData("missing-plan")]
    [InlineData("missing-mapping")]
    [InlineData("inactive-mapping")]
    [InlineData("price-mismatch")]
    [InlineData("currency-mismatch")]
    [InlineData("invalid-currency")]
    [InlineData("mock-mapping")]
    [InlineData("blank-mapping")]
    public async Task Invalid_commercial_data_never_creates_a_checkout(string scenario)
    {
        await using var f = new Fixture();
        var plan = (await f.Db.Planes.FindAsync(BillingSecurityFixture.Basic))!;
        var mapping = await f.Db.BillingPlanProviderMappings.SingleAsync();
        switch (scenario)
        {
            case "zero": plan.PrecioMensual = mapping.UnitAmount = 0; break;
            case "negative": plan.PrecioMensual = mapping.UnitAmount = -1; break;
            case "fractional-cent": plan.PrecioMensual = mapping.UnitAmount = 10.001m; break;
            case "inactive-plan": plan.Activo = false; break;
            case "missing-plan": f.Db.BillingPlanProviderMappings.Remove(mapping); f.Db.Planes.Remove(plan); break;
            case "missing-mapping": f.Db.BillingPlanProviderMappings.Remove(mapping); break;
            case "inactive-mapping": mapping.IsActive = false; break;
            case "price-mismatch": mapping.UnitAmount = 9; break;
            case "currency-mismatch": mapping.Currency = "EUR"; break;
            case "invalid-currency": plan.MonedaCodigo = mapping.Currency = "US1"; break;
            case "mock-mapping": mapping.ExternalPlanId = "mock_price_123"; break;
            case "blank-mapping": mapping.ExternalPlanId = " "; break;
        }
        await f.Db.SaveChangesAsync();

        var result = await f.Service.CreateCheckoutSessionAsync(f.Request());

        result.IsFailure.Should().BeTrue();
        if (scenario is not "inactive-plan" and not "missing-plan")
            result.ErrorCode.Should().Be("BILLING_CHECKOUT_MAPPING_INVALID");
        f.AssertNoEffect();
    }

    [Theory]
    [InlineData("disabled")]
    [InlineData("null-checkout")]
    [InlineData("null-default-provider")]
    [InlineData("blank-default-provider")]
    [InlineData("blank-checkout-provider")]
    [InlineData("mock")]
    [InlineData("missing-provider")]
    [InlineData("unknown-method")]
    [InlineData("missing-account")]
    [InlineData("missing-beneficiary")]
    [InlineData("unsafe-success")]
    [InlineData("unsafe-cancel")]
    [InlineData("redirect-mismatch")]
    public async Task Unavailable_configuration_or_provider_fallback_is_rejected(string scenario)
    {
        await using var f = new Fixture();
        var request = f.Request();
        switch (scenario)
        {
            case "disabled": f.Options.Checkout.Enabled = false; break;
            case "null-checkout": f.Options.Checkout = null!; break;
            case "null-default-provider": f.Options.Provider = null!; request = request with { Metodo = null }; break;
            case "blank-default-provider": f.Options.Provider = " "; request = request with { Metodo = null }; break;
            case "blank-checkout-provider": f.Options.Checkout.Provider = " "; break;
            case "mock": f.Options.Checkout.Provider = "Mock"; break;
            case "missing-provider": f.Options.Checkout.Provider = "NotRegistered"; break;
            case "unknown-method": request = request with { Metodo = "NotRegistered" }; break;
            case "missing-account": f.Options.Checkout.ProviderAccountId = ""; break;
            case "missing-beneficiary": f.Options.Checkout.BeneficiaryId = ""; break;
            case "unsafe-success": f.Options.Checkout.SuccessUrl = "http://billing.example.invalid/success"; break;
            case "unsafe-cancel": f.Options.Checkout.CancelUrl = "http://billing.example.invalid/cancel"; break;
            case "redirect-mismatch": request = request with { ReturnUrl = "https://untrusted.example.invalid/return" }; break;
        }

        var result = await f.Service.CreateCheckoutSessionAsync(request);

        result.ErrorCode.Should().Be(scenario == "redirect-mismatch"
            ? "BILLING_CHECKOUT_RETURN_URL_INVALID" : "BILLING_CHECKOUT_UNAVAILABLE");
        f.AssertNoEffect();
    }

    [Fact]
    public async Task Provider_without_explicit_checkout_capability_cannot_use_legacy_checkout()
    {
        await using var f = new Fixture();
        var legacy = NSubstitute.Substitute.For<IPaymentProvider>();
        NSubstitute.SubstituteExtensions.Returns(legacy.ProviderName, "Wompi");
        var options = Microsoft.Extensions.Options.Options.Create(f.Options);
        var resolver = new PaymentProviderResolver([legacy, new MockPaymentProvider()], options);
        var service = new BillingService(f.Db, resolver, f.Base.Email, options, f.Base.Identity(BillingSecurityFixture.AdminA));

        var result = await service.CreateCheckoutSessionAsync(f.Request());

        result.ErrorCode.Should().Be("BILLING_CHECKOUT_UNAVAILABLE");
        f.AssertNoEffect();
        NSubstitute.SubstituteExtensions.ReceivedCalls(legacy).Should().OnlyContain(call => call.GetMethodInfo().Name == "get_ProviderName");
    }

    [Fact]
    public async Task Timeout_leaves_durable_reconciliation_and_replay_never_retries_provider()
    {
        await using var f = new Fixture();
        f.Provider.OnCheckout = _ => throw new TimeoutException("Synthetic ambiguous response");

        var first = await f.Service.CreateCheckoutSessionAsync(f.Request());
        var replay = await f.Service.CreateCheckoutSessionAsync(f.Request());

        first.ErrorCode.Should().Be("BILLING_RECONCILIATION_REQUIRED");
        replay.ErrorCode.Should().Be("BILLING_RECONCILIATION_REQUIRED");
        f.Provider.CheckoutCalls.Should().Be(1);
        (await f.Db.Set<BillingCheckoutIntent>().SingleAsync()).Status.Should().Be(BillingCheckoutStatuses.RequiresReconciliation);
        f.Db.BillingPayments.Should().BeEmpty();
        f.Db.EmpresaPlanes.Should().BeEmpty();
    }

    [Fact]
    public async Task Processing_replay_from_another_context_does_not_repeat_provider_effect()
    {
        await using var f = new Fixture();
        f.Provider.OnCheckout = async request =>
        {
            await using var other = f.Observe();
            var service = f.ServiceFor(other, BillingSecurityFixture.AdminA);
            var replay = await service.CreateCheckoutSessionAsync(f.Request());
            replay.ErrorCode.Should().Be("BILLING_CHECKOUT_PENDING");
            return FakeProvider.Accept(request);
        };

        var result = await f.Service.CreateCheckoutSessionAsync(f.Request());

        result.IsSuccess.Should().BeTrue(result.Error);
        f.Provider.CheckoutCalls.Should().Be(1);
    }

    [Fact]
    public async Task Checkout_query_cannot_expose_another_tenants_intent()
    {
        await using var f = new Fixture();
        var created = await f.Service.CreateCheckoutSessionAsync(f.Request());
        created.IsSuccess.Should().BeTrue(created.Error);
        var id = created.Value!.CorrelationId!.Value;
        var own = await f.Service.GetCheckoutAsync(BillingSecurityFixture.EmpresaA, id);
        own.IsSuccess.Should().BeTrue(own.Error);
        own.Value!.Amount.Should().Be(10);
        var foreign = f.ServiceFor(f.Db, BillingSecurityFixture.AdminB);

        var selectedForeignCompany = await foreign.GetCheckoutAsync(BillingSecurityFixture.EmpresaA, id);
        var foreignId = await foreign.GetCheckoutAsync(BillingSecurityFixture.EmpresaB, id);

        selectedForeignCompany.ErrorCode.Should().Be("BILLING_FORBIDDEN");
        selectedForeignCompany.Value.Should().BeNull();
        foreignId.IsFailure.Should().BeTrue();
        foreignId.Value.Should().BeNull();
    }

    [Theory]
    [InlineData("trial")]
    [InlineData("transfer")]
    [InlineData("change-plan")]
    [InlineData("cancel")]
    [InlineData("portal")]
    public async Task Unresolved_checkout_blocks_other_billing_mutations(string mutation)
    {
        await using var f = new Fixture();
        (await f.Service.CreateCheckoutSessionAsync(f.Request())).IsSuccess.Should().BeTrue();

        Result result = mutation switch
        {
            "trial" => await f.Service.StartTrialAsync(new(BillingSecurityFixture.EmpresaA, BillingSecurityFixture.Basic, "synthetic@example.invalid")),
            "transfer" => await f.Service.IniciarTransferenciaAsync(new(BillingSecurityFixture.EmpresaA, BillingSecurityFixture.Basic)),
            "change-plan" => await f.Service.ChangePlanAsync(new(BillingSecurityFixture.EmpresaA, BillingSecurityFixture.Pro)),
            "cancel" => await f.Service.CancelSubscriptionAsync(new(BillingSecurityFixture.EmpresaA)),
            "portal" => await f.Service.GetPortalUrlAsync(BillingSecurityFixture.EmpresaA),
            _ => throw new InvalidOperationException()
        };

        result.ErrorCode.Should().Be("BILLING_CHECKOUT_PENDING");
        f.Provider.CheckoutCalls.Should().Be(1);
        f.Db.BillingPayments.Should().BeEmpty();
        f.Db.EmpresaPlanes.Should().BeEmpty();
    }

    [Theory]
    [InlineData("provider")]
    [InlineData("account")]
    [InlineData("session")]
    [InlineData("redirect")]
    [InlineData("redirect-credentials")]
    [InlineData("expired")]
    [InlineData("failure")]
    [InlineData("null-session")]
    public async Task Invalid_provider_response_is_quarantined_without_exposing_redirect_or_retrying(string scenario)
    {
        await using var f = new Fixture();
        f.Provider.OnCheckout = request =>
        {
            var session = FakeProvider.Accept(request).Value!;
            var response = scenario switch
            {
                "provider" => Result<ProviderCheckoutSession>.Ok(session with { Provider = "PayPal" }),
                "account" => Result<ProviderCheckoutSession>.Ok(session with { ProviderAccountId = "different-account" }),
                "session" => Result<ProviderCheckoutSession>.Ok(session with { SessionId = " " }),
                "redirect" => Result<ProviderCheckoutSession>.Ok(session with { RedirectUrl = "http://pay.example.invalid/checkout" }),
                "redirect-credentials" => Result<ProviderCheckoutSession>.Ok(session with { RedirectUrl = "https://synthetic:unused@pay.example.invalid/checkout" }),
                "expired" => Result<ProviderCheckoutSession>.Ok(session with { ExpiresAt = DateTime.UtcNow.AddMinutes(-1) }),
                "failure" => Result<ProviderCheckoutSession>.Fail("Synthetic provider rejection", "SYNTHETIC_REJECTION"),
                "null-session" => Result<ProviderCheckoutSession>.Ok(null!),
                _ => throw new InvalidOperationException()
            };
            return Task.FromResult(response);
        };

        var first = await f.Service.CreateCheckoutSessionAsync(f.Request());
        var replay = await f.Service.CreateCheckoutSessionAsync(f.Request());
        var intent = await f.Db.Set<BillingCheckoutIntent>().SingleAsync();
        var query = await f.Service.GetCheckoutAsync(BillingSecurityFixture.EmpresaA, intent.CorrelationId);

        first.ErrorCode.Should().Be("BILLING_RECONCILIATION_REQUIRED");
        first.Value!.RedirectUrl.Should().BeNullOrEmpty();
        replay.ErrorCode.Should().Be("BILLING_RECONCILIATION_REQUIRED");
        intent.Status.Should().Be(BillingCheckoutStatuses.RequiresReconciliation);
        intent.ProviderAcknowledgedAt.Should().BeNull();
        intent.ExternalCheckoutId.Should().BeNull();
        query.Value!.RedirectUrl.Should().BeNull();
        f.Provider.CheckoutCalls.Should().Be(1);
        f.Db.BillingPayments.Should().BeEmpty();
        f.Db.EmpresaPlanes.Should().BeEmpty();
    }

    [Fact]
    public async Task Cancellation_during_provider_preserves_reference_and_quarantine_using_independent_token()
    {
        await using var f = new Fixture();
        using var cancellation = new CancellationTokenSource();
        f.Provider.OnCheckout = _ =>
        {
            cancellation.Cancel();
            throw new OperationCanceledException(cancellation.Token);
        };

        var result = await f.Service.CreateCheckoutSessionAsync(f.Request(), cancellation.Token);
        var replay = await f.Service.CreateCheckoutSessionAsync(f.Request());

        result.ErrorCode.Should().Be("BILLING_RECONCILIATION_REQUIRED");
        result.Value!.CorrelationId.Should().NotBeNull();
        replay.ErrorCode.Should().Be("BILLING_RECONCILIATION_REQUIRED");
        replay.Value!.CorrelationId.Should().Be(result.Value.CorrelationId);
        await using var observer = f.Observe();
        (await observer.Set<BillingCheckoutIntent>().SingleAsync()).Status.Should().Be(BillingCheckoutStatuses.RequiresReconciliation);
        f.Provider.CheckoutCalls.Should().Be(1);
        observer.BillingPayments.Should().BeEmpty();
        observer.EmpresaPlanes.Should().BeEmpty();
    }

    [Theory]
    [InlineData(BillingCheckoutStatuses.Processing)]
    [InlineData(BillingCheckoutStatuses.AwaitingPayment)]
    public async Task Expired_lease_or_session_requires_reconciliation_without_retry_or_redirect(string status)
    {
        await using var f = new Fixture();
        (await f.Service.CreateCheckoutSessionAsync(f.Request())).IsSuccess.Should().BeTrue();
        var intent = await f.Db.Set<BillingCheckoutIntent>().SingleAsync();
        intent.Status = status;
        intent.LeaseExpiresAt = DateTime.UtcNow.AddMinutes(-1);
        intent.ExpiresAt = DateTime.UtcNow.AddMinutes(-1);
        if (status == BillingCheckoutStatuses.Processing)
        {
            intent.ProviderAcknowledgedAt = null;
            intent.ExternalCheckoutId = null;
            intent.RedirectUrl = null;
        }
        await f.Db.SaveChangesAsync();

        await using var observer = f.Observe();
        var service = f.ServiceFor(observer, BillingSecurityFixture.AdminA);
        var replay = await service.CreateCheckoutSessionAsync(f.Request());
        var query = await service.GetCheckoutAsync(BillingSecurityFixture.EmpresaA, intent.CorrelationId);
        var newRequest = await service.CreateCheckoutSessionAsync(f.Request() with { IdempotencyKey = "synthetic-different-key" });

        replay.ErrorCode.Should().Be("BILLING_RECONCILIATION_REQUIRED");
        replay.Value!.RedirectUrl.Should().BeNullOrEmpty();
        query.Value!.Status.Should().Be(BillingCheckoutStatuses.RequiresReconciliation);
        query.Value.RedirectUrl.Should().BeNull();
        newRequest.ErrorCode.Should().Be("BILLING_CHECKOUT_PENDING");
        f.Provider.CheckoutCalls.Should().Be(1);
        observer.Set<BillingCheckoutIntent>().Should().ContainSingle();
    }
    [Fact]
    public async Task Replay_preserves_snapshot_when_server_return_url_and_default_provider_change()
    {
        await using var f = new Fixture();
        var request = f.Request() with { Metodo = null };
        var first = await f.Service.CreateCheckoutSessionAsync(request);
        first.IsSuccess.Should().BeTrue(first.Error);
        f.Options.Provider = "Mock";
        f.Options.Checkout.SuccessUrl = "https://billing.example.invalid/new-success";
        f.Options.Checkout.CancelUrl = "https://billing.example.invalid/new-cancel";

        var replay = await f.Service.CreateCheckoutSessionAsync(request with { ReturnUrl = f.Options.Checkout.SuccessUrl });

        replay.IsSuccess.Should().BeTrue(replay.Error);
        replay.Value.Should().BeEquivalentTo(first.Value);
        f.Provider.CheckoutCalls.Should().Be(1);
        var intent = await f.Db.Set<BillingCheckoutIntent>().SingleAsync();
        intent.Provider.Should().Be("Wompi");
        intent.SuccessUrl.Should().Be(Fixture.Success);
        intent.CancelUrl.Should().Be(Fixture.Cancel);
    }
    [Theory]
    [InlineData(BillingCheckoutStatuses.PaymentVerifiedSandbox)]
    [InlineData(BillingCheckoutStatuses.Completed)]
    public async Task Verified_or_completed_replay_recovers_same_identity_without_payment_redirect_or_another_post(string status)
    {
        await using var f = new Fixture();
        var created = await f.Service.CreateCheckoutSessionAsync(f.Request());
        created.IsSuccess.Should().BeTrue(created.Error);
        var intent = await f.Db.Set<BillingCheckoutIntent>().SingleAsync();
        intent.Status = status;
        await f.Db.SaveChangesAsync();
        await using var observer = f.Observe();
        var service = f.ServiceFor(observer, BillingSecurityFixture.AdminA);

        var replay = await service.CreateCheckoutSessionAsync(f.Request());
        var query = await service.GetCheckoutAsync(BillingSecurityFixture.EmpresaA, intent.CorrelationId);

        replay.IsSuccess.Should().BeTrue(replay.Error);
        replay.Value!.CorrelationId.Should().Be(created.Value!.CorrelationId);
        replay.Value.SessionId.Should().Be(created.Value.SessionId);
        replay.Value.Status.Should().Be(status);
        replay.Value.RedirectUrl.Should().BeEmpty();
        query.IsSuccess.Should().BeTrue(query.Error);
        query.Value!.CorrelationId.Should().Be(intent.CorrelationId);
        query.Value.SessionId.Should().Be(created.Value.SessionId);
        query.Value.Status.Should().Be(status);
        query.Value.RedirectUrl.Should().BeNull();
        f.Provider.CheckoutCalls.Should().Be(1);
        observer.Set<BillingCheckoutIntent>().Should().ContainSingle();
        observer.BillingPayments.Should().BeEmpty();
        observer.EmpresaPlanes.Should().BeEmpty();
    }
    private sealed class Fixture : IAsyncDisposable
    {
        public const string Key = "synthetic-checkout-key-0001";
        public const string Account = "synthetic-account";
        public const string Success = "https://billing.example.invalid/success";
        public const string Cancel = "https://billing.example.invalid/cancel";
        public BillingSecurityFixture Base { get; } = new();
        public NeoStpDbContext Db => Base.Db;
        public FakeProvider Provider { get; } = new();
        public BillingOptions Options { get; } = new()
        {
            Provider = "Wompi",
            Checkout = new()
            {
                Enabled = true, Provider = "Wompi", ProviderAccountId = Account,
                BeneficiaryId = "synthetic-beneficiary", SuccessUrl = Success, CancelUrl = Cancel, LeaseSeconds = 120
            }
        };
        public BillingService Service => ServiceFor(Db, BillingSecurityFixture.AdminA);
        public Fixture()
        {
            Db.BillingPlanProviderMappings.Add(new()
            {
                PlanId = BillingSecurityFixture.Basic, Provider = "Wompi", ExternalPlanId = "synthetic-price-basic",
                Currency = "USD", UnitAmount = 10, IsActive = true
            });
            Db.SaveChanges();
        }
        public CreateCheckoutRequest Request() => new(BillingSecurityFixture.EmpresaA, BillingSecurityFixture.Basic, Success, "Wompi", Key);
        public NeoStpDbContext Observe() => new(new DbContextOptionsBuilder<NeoStpDbContext>().UseInMemoryDatabase(Base.DatabaseName, Base.Store).Options);
        public BillingService ServiceFor(NeoStpDbContext db, int userId)
        {
            var options = Microsoft.Extensions.Options.Options.Create(Options);
            var resolver = new PaymentProviderResolver([Provider, new MockPaymentProvider(), new TransferenciaPaymentProvider()], options);
            return new(db, resolver, Base.Email, options, Base.Identity(userId));
        }
        public void AssertNoEffect()
        {
            Provider.CheckoutCalls.Should().Be(0);
            Provider.CustomerCalls.Should().Be(0);
            Provider.LegacyCheckoutCalls.Should().Be(0);
            Db.Set<BillingCheckoutIntent>().Should().BeEmpty();
            Db.BillingPayments.Should().BeEmpty();
            Db.EmpresaPlanes.Should().BeEmpty();
        }
        public ValueTask DisposeAsync() => Base.DisposeAsync();
    }

    private sealed class FakeProvider : IPaymentProvider, IBillingCheckoutProvider
    {
        public string ProviderName => "Wompi";
        public int CheckoutCalls { get; private set; }
        public int CustomerCalls { get; private set; }
        public int LegacyCheckoutCalls { get; private set; }
        public Func<ProviderCheckoutRequest, Task<Result<ProviderCheckoutSession>>>? OnCheckout { get; set; }
        public static Result<ProviderCheckoutSession> Accept(ProviderCheckoutRequest request)
            => Result<ProviderCheckoutSession>.Ok(new("Wompi", request.ProviderAccountId, "synthetic-session", "https://pay.example.invalid/checkout", DateTime.UtcNow.AddMinutes(15)));
        public Task<Result<ProviderCheckoutSession>> CreateCheckoutAsync(ProviderCheckoutRequest request, CancellationToken ct = default)
        {
            CheckoutCalls++;
            return OnCheckout?.Invoke(request) ?? Task.FromResult(Accept(request));
        }
        public Task<Result<string>> CreateCustomerAsync(string email, int empresaId, CancellationToken ct = default)
        {
            CustomerCalls++;
            return Task.FromResult(Result<string>.Ok("synthetic-customer"));
        }
        public Task<Result<CheckoutSessionResult>> CreateCheckoutSessionAsync(string customerId, string externalPlanId, string successUrl, string cancelUrl, CancellationToken ct = default)
        {
            LegacyCheckoutCalls++;
            throw new InvalidOperationException("Legacy amount-less checkout must never be used.");
        }
        public Task<Result<BillingPortalResult>> CreatePortalSessionAsync(string customerId, string returnUrl, CancellationToken ct = default)
            => throw new InvalidOperationException("Unexpected portal mutation.");
        public Task<Result<string>> ChangePlanAsync(string externalSubscriptionId, string newExternalPlanId, CancellationToken ct = default)
            => throw new InvalidOperationException("Unexpected plan mutation.");
        public Task<Result> CancelSubscriptionAsync(string externalSubscriptionId, bool atPeriodEnd, CancellationToken ct = default)
            => throw new InvalidOperationException("Unexpected cancellation mutation.");
    }
}
