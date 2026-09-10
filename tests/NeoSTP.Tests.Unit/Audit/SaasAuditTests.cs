using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NeoSTP.Application.Billing.Dtos;
using NeoSTP.Domain.Core.Billing;
using NeoSTP.Tests.Unit.Billing;

namespace NeoSTP.Tests.Unit.Audit;

public sealed class SaasAuditTests
{
    [Theory]
    [InlineData(SubscriptionStatus.Canceled)]
    [InlineData(SubscriptionStatus.Expired)]
    public async Task UsedTrial_CannotBeRestartedAfterCancellationOrExpiry(string finalState)
    {
        await using var f = new BillingSecurityFixture();
        var service = f.Service(BillingSecurityFixture.AdminA);
        (await service.StartTrialAsync(new(BillingSecurityFixture.EmpresaA, BillingSecurityFixture.Basic, "audit@example.invalid"))).IsSuccess.Should().BeTrue();
        var originalEnd = (await f.Db.EmpresaPlanes.SingleAsync()).FechaFin;
        if (finalState == SubscriptionStatus.Canceled)
            (await service.CancelSubscriptionAsync(new(BillingSecurityFixture.EmpresaA, false))).IsSuccess.Should().BeTrue();
        else
        {
            (await f.Db.BillingSubscriptions.SingleAsync()).Status = finalState;
            await f.Db.SaveChangesAsync();
        }
        var endAfterClosure = (await f.Db.EmpresaPlanes.SingleAsync()).FechaFin;
        if (finalState == SubscriptionStatus.Canceled)
        {
            endAfterClosure.Should().BeOnOrBefore(originalEnd!.Value);
            (await f.Db.EmpresaPlanes.SingleAsync()).EstadoCodigo.Should().Be("CANCELADO");
        }
        var retry = await service.StartTrialAsync(new(BillingSecurityFixture.EmpresaA, BillingSecurityFixture.Pro, "audit@example.invalid"));
        retry.ErrorCode.Should().Be("BILLING_TRIAL_ALREADY_USED");
        (await f.Db.BillingSubscriptions.CountAsync()).Should().Be(1);
        (await f.Db.EmpresaPlanes.SingleAsync()).FechaFin.Should().Be(endAfterClosure);
        (await f.Db.EmpresaPlanes.SingleAsync()).PlanId.Should().Be(BillingSecurityFixture.Basic);
    }

    // Acceptance regressions: synthetic InMemory company, real service/identity accessor,
    // no live company, gateway, SMTP, SQL, host startup or seeder.
    [Fact]
    public async Task ChangingTrialPlan_PreservesOriginalExpiry_WithoutGrantingPaidOrUnlimitedPeriod()
    {
        await using var f = new BillingSecurityFixture();
        var service = f.Service(BillingSecurityFixture.AdminA);
        var trial = await service.StartTrialAsync(new(BillingSecurityFixture.EmpresaA, BillingSecurityFixture.Basic, "audit@example.invalid"));
        trial.IsSuccess.Should().BeTrue();
        var originalEnd = (await f.Db.EmpresaPlanes.SingleAsync()).FechaFin;
        originalEnd.Should().NotBeNull();
        (await service.ChangePlanAsync(new(BillingSecurityFixture.EmpresaA, BillingSecurityFixture.Pro))).IsSuccess.Should().BeTrue();
        (await f.Db.EmpresaPlanes.AsNoTracking().SingleAsync()).FechaFin.Should().Be(originalEnd);
        var subscription = await f.Db.BillingSubscriptions.AsNoTracking().SingleAsync();
        subscription.TrialEnd.Should().Be(originalEnd);
        subscription.CurrentPeriodEnd.Should().BeNull();
        subscription.Status.Should().Be(SubscriptionStatus.Trialing);
        (await f.Db.BillingPayments.CountAsync()).Should().Be(0);
    }

    [Theory]
    [InlineData(SubscriptionStatus.Incomplete, false)]
    [InlineData(SubscriptionStatus.Suspended, false)]
    [InlineData(SubscriptionStatus.PastDue, false)]
    [InlineData(SubscriptionStatus.Active, false)]
    [InlineData(SubscriptionStatus.Active, true)]
    [InlineData(SubscriptionStatus.Trialing, true)]
    public async Task ChangingPlan_WithoutValidPeriod_DoesNotActivateLicense(string state, bool expired)
    {
        await using var f = new BillingSecurityFixture();
        var payment = await f.Pending(BillingSecurityFixture.EmpresaA);
        payment.Subscription.Status = state;
        payment.Subscription.ExternalSubscriptionId = "synthetic-only";
        payment.Subscription.TrialEnd = DateTime.UtcNow.AddDays(-1);
        payment.Subscription.CurrentPeriodEnd = expired ? DateTime.UtcNow.AddDays(-1) : null;
        await f.Db.SaveChangesAsync();
        var result = await f.Service(BillingSecurityFixture.AdminA).ChangePlanAsync(
            new(BillingSecurityFixture.EmpresaA, BillingSecurityFixture.Pro));
        result.ErrorCode.Should().Be("BILLING_SUBSCRIPTION_NOT_CURRENT");
        (await f.Db.EmpresaPlanes.CountAsync()).Should().Be(0);
        payment.Subscription.PlanId.Should().Be(BillingSecurityFixture.Basic);
    }

    [Fact]
    public async Task OfflinePaidPlan_RequiresVerificationBeforeChangingLicense()
    {
        await using var f = new BillingSecurityFixture();
        var payment = await f.Pending(BillingSecurityFixture.EmpresaA);
        var central = f.Service(BillingSecurityFixture.Central);
        (await central.ConfirmarTransferenciaAsync(payment.Id, "untrusted-actor")).IsSuccess.Should().BeTrue();
        var result = await f.Service(BillingSecurityFixture.AdminA).ChangePlanAsync(new(BillingSecurityFixture.EmpresaA, BillingSecurityFixture.Pro));
        result.ErrorCode.Should().Be("BILLING_PAYMENT_REQUIRED");
        (await f.Db.EmpresaPlanes.SingleAsync()).PlanId.Should().Be(BillingSecurityFixture.Basic);
    }
}
