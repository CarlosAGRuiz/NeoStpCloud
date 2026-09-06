using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NeoSTP.Application.Billing;
using NeoSTP.Domain.Core.Billing;
using NeoSTP.Domain.Core.Licenciamiento;
using NeoSTP.Infrastructure.Billing;
using NeoSTP.Infrastructure.Persistence;

namespace NeoSTP.Tests.Unit.Billing;

/// <summary>Synthetic durable production evidence in InMemory only; no real payment, host or network.</summary>
public sealed class BillingPaymentApplicationTests
{
    [Fact]
    public async Task First_purchase_creates_one_linked_payment_subscription_license_and_ledger_for_the_intent_company()
    {
        await using var f = new Fixture();
        await f.Capture();

        var result = await f.Processor.ApplyVerifiedPaymentAsync(f.Receipt.ReceiptId);

        result.IsSuccess.Should().BeTrue(result.Error);
        await using var observed = f.Observe();
        var application = await observed.BillingPaymentApplications.SingleAsync();
        var payment = await observed.BillingPayments.SingleAsync();
        var subscription = await observed.BillingSubscriptions.SingleAsync();
        var customer = await observed.BillingCustomers.SingleAsync();
        var license = await observed.EmpresaPlanes.SingleAsync();
        var intent = await observed.BillingCheckoutIntents.SingleAsync();
        application.BillingCheckoutIntentId.Should().Be(f.Intent.Id);
        application.BillingPaymentNotificationId.Should().Be(f.Receipt.Id);
        application.BillingPaymentId.Should().Be(payment.Id);
        application.BillingSubscriptionId.Should().Be(subscription.Id);
        application.EmpresaPlanId.Should().Be(license.Id);
        application.CommercialSnapshotJson.Should().Be(f.Intent.CommercialSnapshotJson);
        application.ModuleIdsJson.Should().Be("[995001]");
        payment.ExternalPaymentId.Should().Be(f.Receipt.TransactionId.ToString("D"));
        payment.Status.Should().Be("SUCCEEDED"); payment.Metodo.Should().Be("WOMPI");
        payment.Amount.Should().Be(10m); payment.Currency.Should().Be("USD");
        payment.PaidAt.Should().Be(f.PaidAt.UtcDateTime);
        subscription.Status.Should().Be(SubscriptionStatus.Active);
        subscription.PlanId.Should().Be(BillingSecurityFixture.Basic);
        subscription.CurrentPeriodStart.Should().Be(f.PaidAt.UtcDateTime);
        subscription.CurrentPeriodEnd.Should().Be(f.PaidAt.UtcDateTime.AddMonths(1));
        license.FechaFin.Should().Be(subscription.CurrentPeriodEnd);
        license.EstadoCodigo.Should().Be("ACTIVO");
        customer.EmpresaId.Should().Be(BillingSecurityFixture.EmpresaA);
        license.EmpresaId.Should().Be(BillingSecurityFixture.EmpresaA);
        intent.Status.Should().Be(BillingCheckoutStatuses.Completed);
        intent.CompletedAt.Should().NotBeNull();
        intent.BillingCustomerId.Should().BeNull("pre-checkout identities are immutable");
        intent.BillingSubscriptionId.Should().BeNull(); intent.EmpresaPlanId.Should().BeNull();
        (await observed.EmpresaModulos.SingleAsync()).Activo.Should().BeTrue();
        observed.EmpresaPlanes.Where(x => x.EmpresaId == BillingSecurityFixture.EmpresaB).Should().BeEmpty();
    }

    [Fact]
    public async Task Same_receipt_replay_after_cancellation_does_not_reactivate_or_extend_access()
    {
        await using var f = new Fixture(); await f.Capture();
        (await f.Processor.ApplyVerifiedPaymentAsync(f.Receipt.ReceiptId)).IsSuccess.Should().BeTrue();
        var subscription = await f.Db.BillingSubscriptions.SingleAsync();
        var license = await f.Db.EmpresaPlanes.SingleAsync();
        var end = subscription.CurrentPeriodEnd;
        subscription.Status = SubscriptionStatus.Canceled; subscription.CanceledAt = DateTime.UtcNow;
        license.EstadoCodigo = "CANCELADO";
        await f.Db.SaveChangesAsync();

        var replay = await f.Processor.ApplyVerifiedPaymentAsync(f.Receipt.ReceiptId);

        replay.IsSuccess.Should().BeTrue(replay.Error);
        f.Db.BillingPayments.Should().ContainSingle(); f.Db.BillingPaymentApplications.Should().ContainSingle();
        (await f.Db.BillingSubscriptions.SingleAsync()).Status.Should().Be(SubscriptionStatus.Canceled);
        (await f.Db.BillingSubscriptions.SingleAsync()).CurrentPeriodEnd.Should().Be(end);
        (await f.Db.EmpresaPlanes.SingleAsync()).EstadoCodigo.Should().Be("CANCELADO");
    }

    [Fact]
    public async Task Another_captured_transaction_for_same_checkout_requires_reconciliation_without_second_payment()
    {
        await using var f = new Fixture(); await f.Capture();
        (await f.Processor.ApplyVerifiedPaymentAsync(f.Receipt.ReceiptId)).IsSuccess.Should().BeTrue();
        var second = f.NewReceipt(); f.Db.BillingPaymentNotifications.Add(second); await f.Db.SaveChangesAsync();

        var result = await f.Processor.ApplyVerifiedPaymentAsync(second.ReceiptId);

        result.ErrorCode.Should().Be("BILLING_ADDITIONAL_PAYMENT_RECONCILIATION");
        f.Db.BillingPayments.Should().ContainSingle(); f.Db.BillingPaymentApplications.Should().ContainSingle();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Same_plan_renewal_extends_from_later_of_previous_end_and_captured_payment(bool unexpired)
    {
        await using var f = new Fixture();
        var previousEnd = f.PaidAt.UtcDateTime.AddDays(unexpired ? 10 : -2);
        await f.ExistingSubscription(SubscriptionStatus.Active, previousEnd);
        await f.Capture();
        var subscriptionId = f.Intent.BillingSubscriptionId; var licenseId = f.Intent.EmpresaPlanId;

        var result = await f.Processor.ApplyVerifiedPaymentAsync(f.Receipt.ReceiptId);

        result.IsSuccess.Should().BeTrue(result.Error);
        var subscription = await f.Db.BillingSubscriptions.SingleAsync();
        var expectedStart = unexpired ? previousEnd : f.PaidAt.UtcDateTime;
        subscription.Id.Should().Be(subscriptionId!.Value);
        subscription.CurrentPeriodStart.Should().Be(expectedStart);
        subscription.CurrentPeriodEnd.Should().Be(expectedStart.AddMonths(1));
        (await f.Db.EmpresaPlanes.SingleAsync()).Id.Should().Be(licenseId!.Value);
        (await f.Db.EmpresaPlanes.SingleAsync()).FechaFin.Should().Be(expectedStart.AddMonths(1));
        f.Db.BillingPayments.Should().ContainSingle(); f.Db.BillingPaymentApplications.Should().ContainSingle();
    }

    [Fact]
    public async Task Current_same_plan_Wompi_trial_converts_from_payment_time_without_adding_remaining_trial_days()
    {
        await using var f = new Fixture();
        await f.ExistingSubscription(SubscriptionStatus.Trialing, f.PaidAt.UtcDateTime.AddDays(9));
        await f.Capture();
        var result = await f.Processor.ApplyVerifiedPaymentAsync(f.Receipt.ReceiptId);
        result.IsSuccess.Should().BeTrue(result.Error);
        var subscription = await f.Db.BillingSubscriptions.SingleAsync();
        subscription.Status.Should().Be(SubscriptionStatus.Active);
        subscription.CurrentPeriodStart.Should().Be(f.PaidAt.UtcDateTime);
        subscription.CurrentPeriodEnd.Should().Be(f.PaidAt.UtcDateTime.AddMonths(1));
    }

    [Theory]
    [InlineData("disabled")]
    [InlineData("sandbox")]
    [InlineData("unverified")]
    [InlineData("intent-sandbox")]
    [InlineData("missing-verified-at")]
    [InlineData("missing-paid-at")]
    [InlineData("account")]
    [InlineData("beneficiary")]
    [InlineData("amount")]
    [InlineData("currency")]
    [InlineData("link")]
    [InlineData("correlation")]
    [InlineData("empty-transaction")]
    [InlineData("future-paid-at")]
    public async Task Untrusted_or_sandbox_capture_cannot_apply_commercial_payment(string scenario)
    {
        await using var f = new Fixture();
        switch (scenario)
        {
            case "disabled": f.Options.PaymentApplication.Enabled = false; break;
            case "sandbox": f.Receipt.IsProduction = false; f.Intent.IsProduction = false; f.Receipt.Status = BillingPaymentNotificationStatuses.VerifiedSandbox; break;
            case "unverified": f.Receipt.Status = BillingPaymentNotificationStatuses.Processing; break;
            case "intent-sandbox": f.Intent.IsProduction = false; break;
            case "missing-verified-at": f.Receipt.VerifiedAt = null; break;
            case "missing-paid-at": f.Receipt.ProviderPaidAt = null; break;
            case "account": f.Receipt.ProviderAccountId = "foreign-account"; break;
            case "beneficiary": f.Receipt.BeneficiaryId = "foreign-app"; break;
            case "amount": f.Receipt.Amount = 11; break;
            case "currency": f.Receipt.Currency = "EUR"; break;
            case "link": f.Receipt.ExternalCheckoutId = "different-link"; break;
            case "correlation": f.Receipt.CheckoutCorrelationId = Guid.NewGuid(); break;
            case "empty-transaction": f.Receipt.TransactionId = Guid.Empty; break;
            case "future-paid-at": f.Receipt.ProviderPaidAt = f.PaidAt.AddDays(1); break;
        }
        await f.Capture();
        var result = await f.Processor.ApplyVerifiedPaymentAsync(f.Receipt.ReceiptId);
        result.ErrorCode.Should().Be(scenario == "disabled" ? "BILLING_APPLICATION_DISABLED" : "BILLING_CAPTURE_NOT_VERIFIED");
        f.AssertNoApplication();
    }

    [Theory]
    [InlineData("price")]
    [InlineData("name")]
    [InlineData("plan-inactive")]
    [InlineData("quota")]
    [InlineData("plan-module")]
    [InlineData("company-status")]
    [InlineData("new-customer")]
    [InlineData("missing-snapshot")]
    public async Task Commercial_state_drift_since_checkout_blocks_application(string drift)
    {
        await using var f = new Fixture(); await f.Capture();
        var plan = (await f.Db.Planes.FindAsync(BillingSecurityFixture.Basic))!;
        switch (drift)
        {
            case "price": plan.PrecioMensual = 11; break;
            case "name": plan.Nombre = "Changed name"; break;
            case "plan-inactive": plan.Activo = false; break;
            case "quota": plan.LimiteUsuarios = 100; break;
            case "plan-module": (await f.Db.PlanModulos.SingleAsync()).Activo = false; break;
            case "company-status": (await f.Db.Empresas.FindAsync(BillingSecurityFixture.EmpresaA))!.EstadoCodigo = "INACTIVA"; break;
            case "new-customer": f.Db.BillingCustomers.Add(new() { EmpresaId = BillingSecurityFixture.EmpresaA, Provider = "Wompi", Email = "synthetic@example.invalid" }); break;
            case "missing-snapshot": f.Intent.CommercialSnapshotJson = ""; break;
        }
        await f.Db.SaveChangesAsync();
        var result = await f.Processor.ApplyVerifiedPaymentAsync(f.Receipt.ReceiptId);
        result.ErrorCode.Should().Be(drift == "company-status" ? "BILLING_TRANSITION_UNSUPPORTED" : "BILLING_COMMERCIAL_SNAPSHOT_CHANGED");
        f.AssertNoApplication();
    }

    [Theory]
    [InlineData("canceled")]
    [InlineData("cancel-at-end")]
    [InlineData("canceled-at")]
    [InlineData("suspended")]
    [InlineData("different-plan")]
    [InlineData("external-subscription")]
    [InlineData("license-end")]
    [InlineData("license-inactive")]
    [InlineData("expired-trial")]
    [InlineData("extra-module")]
    public async Task Unsupported_transition_is_not_silently_applied(string scenario)
    {
        await using var f = new Fixture();
        await f.ExistingSubscription(scenario == "expired-trial" ? SubscriptionStatus.Trialing : SubscriptionStatus.Active,
            f.PaidAt.UtcDateTime.AddDays(scenario == "expired-trial" ? -1 : 10));
        var sub = await f.Db.BillingSubscriptions.SingleAsync(); var license = await f.Db.EmpresaPlanes.SingleAsync();
        switch (scenario)
        {
            case "canceled": sub.Status = SubscriptionStatus.Canceled; break;
            case "cancel-at-end": sub.CancelAtPeriodEnd = true; break;
            case "canceled-at": sub.CanceledAt = f.PaidAt.UtcDateTime; break;
            case "suspended": sub.Status = SubscriptionStatus.Suspended; break;
            case "different-plan": sub.PlanId = BillingSecurityFixture.Pro; break;
            case "external-subscription": sub.ExternalSubscriptionId = "synthetic-external-sub"; break;
            case "license-end": license.FechaFin = license.FechaFin!.Value.AddDays(1); break;
            case "license-inactive": license.EstadoCodigo = "CANCELADO"; break;
            case "extra-module": f.Db.EmpresaModulos.Add(new() { EmpresaId = BillingSecurityFixture.EmpresaA, ModuloId = Fixture.ExtraModule, Activo = true }); break;
        }
        await f.Capture();
        var endBefore = license.FechaFin;
        var result = await f.Processor.ApplyVerifiedPaymentAsync(f.Receipt.ReceiptId);
        result.ErrorCode.Should().Be("BILLING_TRANSITION_UNSUPPORTED");
        f.AssertNoApplication();
        (await f.Db.EmpresaPlanes.SingleAsync()).FechaFin.Should().Be(endBefore);
    }

    [Theory]
    [InlineData("checkout")]
    [InlineData("cancellation")]
    [InlineData("transfer")]
    public async Task Pending_company_operation_blocks_application(string operation)
    {
        await using var f = new Fixture();
        if (operation == "checkout") f.Db.BillingCheckoutIntents.Add(new() { EmpresaId = BillingSecurityFixture.EmpresaA,
            PlanId = BillingSecurityFixture.Basic, CorrelationId = Guid.NewGuid(), Status = BillingCheckoutStatuses.Processing });
        if (operation == "cancellation") f.Db.BillingProviderOperations.Add(new() { EmpresaId = BillingSecurityFixture.EmpresaA,
            PlanId = BillingSecurityFixture.Basic, Provider = "Wompi", Status = BillingProviderOperationStatuses.Pending });
        if (operation == "transfer") await f.Base.Pending(BillingSecurityFixture.EmpresaA);
        await f.Capture();
        var beforePayments = await f.Db.BillingPayments.CountAsync();
        var result = await f.Processor.ApplyVerifiedPaymentAsync(f.Receipt.ReceiptId);
        result.ErrorCode.Should().Be("BILLING_APPLICATION_CONFLICT");
        f.Db.BillingPaymentApplications.Should().BeEmpty();
        (await f.Db.BillingPayments.CountAsync()).Should().Be(beforePayments);
        f.Db.EmpresaPlanes.Should().BeEmpty();
    }

    [Fact]
    public async Task Foreign_company_pending_operation_does_not_block_intent_company_purchase()
    {
        await using var f = new Fixture();
        await f.Base.Pending(BillingSecurityFixture.EmpresaB);
        await f.Capture();
        var result = await f.Processor.ApplyVerifiedPaymentAsync(f.Receipt.ReceiptId);
        result.IsSuccess.Should().BeTrue(result.Error);
        (await f.Db.EmpresaPlanes.SingleAsync()).EmpresaId.Should().Be(BillingSecurityFixture.EmpresaA);
        f.Db.BillingPayments.Count(x => x.Status == "SUCCEEDED").Should().Be(1);
        f.Db.BillingPayments.Count(x => x.Status == "PENDIENTE_VERIFICACION").Should().Be(1);
    }

    [Fact]
    public async Task Snapshot_canonicalizes_decimal_scale_and_excludes_contact_PII()
    {
        await using var f = new Fixture(); await f.Capture();
        var first = f.Intent.CommercialSnapshotJson;
        (await f.Db.Planes.FindAsync(BillingSecurityFixture.Basic))!.PrecioMensual = 10.0000m;
        (await f.Db.Empresas.FindAsync(BillingSecurityFixture.EmpresaA))!.Correo = "synthetic-sensitive@example.invalid";
        await f.Db.SaveChangesAsync();
        var second = await BillingCommercialSnapshot.CaptureAsync(f.Db, f.Intent);
        second.Should().Be(first).And.NotContain("synthetic-sensitive");
    }

    private sealed class Fixture : IAsyncDisposable
    {
        public const int IncludedModule = 995001, ExtraModule = 995002;
        public BillingSecurityFixture Base { get; } = new();
        public NeoStpDbContext Db => Base.Db;
        public DateTimeOffset PaidAt { get; } = DateTimeOffset.UtcNow.AddMinutes(-1);
        public BillingCheckoutIntent Intent { get; }
        public BillingPaymentNotification Receipt { get; }
        public BillingOptions Options { get; } = new() { PaymentApplication = new() { Enabled = true } };
        public BillingPaymentApplicationProcessor Processor => new(Db, Microsoft.Extensions.Options.Options.Create(Options));
        public Fixture()
        {
            Db.Modulos.AddRange(new Modulo { Id = IncludedModule, Codigo = "SYNTHETIC_INCLUDED", Nombre = "Included" },
                new Modulo { Id = ExtraModule, Codigo = "SYNTHETIC_EXTRA", Nombre = "Addon" });
            Db.PlanModulos.Add(new() { PlanId = BillingSecurityFixture.Basic, ModuloId = IncludedModule, Activo = true });
            Intent = new() { CorrelationId = Guid.NewGuid(), EmpresaId = BillingSecurityFixture.EmpresaA,
                PlanId = BillingSecurityFixture.Basic, PlanCode = "AUDIT_BASIC", PlanName = "Basic", Amount = 10m, Currency = "USD",
                Provider = "Wompi", ProviderAccountId = "synthetic-account", BeneficiaryId = "synthetic-app", IsProduction = true,
                ExternalCheckoutId = "12345", Status = BillingCheckoutStatuses.AwaitingPayment, CreatedAt = PaidAt.UtcDateTime.AddMinutes(-1) };
            Db.BillingCheckoutIntents.Add(Intent);
            Receipt = NewReceipt(); Db.BillingPaymentNotifications.Add(Receipt); Db.SaveChanges();
        }
        public BillingPaymentNotification NewReceipt() => new()
        {
            ReceiptId = Guid.NewGuid(), BillingCheckoutIntentId = Intent.Id, TransactionId = Guid.NewGuid(), CheckoutCorrelationId = Intent.CorrelationId,
            Provider = "Wompi", ProviderAccountId = Intent.ProviderAccountId, BeneficiaryId = Intent.BeneficiaryId, IsProduction = true,
            ExternalCheckoutId = Intent.ExternalCheckoutId!, Amount = 10m, Currency = "USD", TransactionAt = PaidAt,
            ProviderPaidAt = PaidAt, VerifiedAt = DateTime.UtcNow, Status = BillingPaymentNotificationStatuses.VerifiedCapturedProduction
        };
        public async Task Capture()
        {
            await Db.SaveChangesAsync();
            Intent.CommercialSnapshotJson = await BillingCommercialSnapshot.CaptureAsync(Db, Intent);
            await Db.SaveChangesAsync();
        }
        public async Task ExistingSubscription(string status, DateTime periodEnd)
        {
            var customer = new BillingCustomer { EmpresaId = BillingSecurityFixture.EmpresaA, Provider = "Wompi", Email = "synthetic@example.invalid" };
            var subscription = new BillingSubscription { Customer = customer, PlanId = BillingSecurityFixture.Basic, Status = status,
                CurrentPeriodStart = PaidAt.UtcDateTime.AddMonths(-1), CurrentPeriodEnd = status == SubscriptionStatus.Active ? periodEnd : null,
                TrialStart = PaidAt.UtcDateTime.AddDays(-3), TrialEnd = periodEnd };
            var license = new EmpresaPlan { EmpresaId = BillingSecurityFixture.EmpresaA, PlanId = BillingSecurityFixture.Basic,
                FechaInicio = PaidAt.UtcDateTime.AddMonths(-1), FechaFin = periodEnd, EstadoCodigo = "ACTIVO" };
            Db.BillingSubscriptions.Add(subscription); Db.EmpresaPlanes.Add(license); await Db.SaveChangesAsync();
            Intent.BillingCustomerId = customer.Id; Intent.BillingSubscriptionId = subscription.Id; Intent.EmpresaPlanId = license.Id;
        }
        public NeoStpDbContext Observe() => new(new DbContextOptionsBuilder<NeoStpDbContext>().UseInMemoryDatabase(Base.DatabaseName, Base.Store).Options);
        public void AssertNoApplication() { Db.BillingPayments.Should().BeEmpty(); Db.BillingPaymentApplications.Should().BeEmpty(); }
        public ValueTask DisposeAsync() => Base.DisposeAsync();
    }
}
