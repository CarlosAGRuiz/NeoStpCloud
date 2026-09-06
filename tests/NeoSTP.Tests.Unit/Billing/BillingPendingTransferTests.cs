using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NeoSTP.Domain.Core.Billing;

namespace NeoSTP.Tests.Unit.Billing;

public sealed class BillingPendingTransferTests
{
    [Fact]
    public async Task CheapPendingPayment_CannotBeRetargetedToExpensivePlan_OrDuplicated()
    {
        await using var f = new BillingSecurityFixture();
        var admin = f.Service(BillingSecurityFixture.AdminA);
        var first = await admin.IniciarTransferenciaAsync(new(BillingSecurityFixture.EmpresaA, BillingSecurityFixture.Basic));
        first.IsSuccess.Should().BeTrue();
        (await admin.IniciarTransferenciaAsync(new(BillingSecurityFixture.EmpresaA, BillingSecurityFixture.Pro))).ErrorCode.Should().Be("BILLING_TRANSFER_PENDING");
        (await admin.IniciarTransferenciaAsync(new(BillingSecurityFixture.EmpresaA, BillingSecurityFixture.Basic))).ErrorCode.Should().Be("BILLING_TRANSFER_PENDING");
        (await f.Db.BillingPayments.CountAsync()).Should().Be(1);
        (await f.Db.BillingSubscriptions.SingleAsync()).PlanId.Should().Be(BillingSecurityFixture.Basic);
        (await f.Service(BillingSecurityFixture.Central).ConfirmarTransferenciaAsync(first.Value!.PaymentId, "forged")).IsSuccess.Should().BeTrue();
        (await f.Db.EmpresaPlanes.SingleAsync()).PlanId.Should().Be(BillingSecurityFixture.Basic);
    }

    [Theory]
    [InlineData("amount")]
    [InlineData("currency")]
    [InlineData("inactive")]
    public async Task LegacyPendingTransfer_WhosePlanNoLongerMatches_CannotActivateLicense(string mismatch)
    {
        await using var f = new BillingSecurityFixture();
        var payment = await f.Pending(BillingSecurityFixture.EmpresaA);
        if (mismatch == "amount") payment.Subscription.PlanId = BillingSecurityFixture.Pro;
        if (mismatch == "currency") payment.Currency = "EUR";
        if (mismatch == "inactive") (await f.Db.Planes.FindAsync(BillingSecurityFixture.Basic))!.Activo = false;
        await f.Db.SaveChangesAsync();
        var result = await f.Service(BillingSecurityFixture.Central).ConfirmarTransferenciaAsync(payment.Id, "forged");
        result.ErrorCode.Should().Be("BILLING_TRANSFER_PLAN_MISMATCH");
        payment.Status.Should().Be("PENDIENTE_VERIFICACION");
        payment.VerificadoPor.Should().BeNull();
        (await f.Db.EmpresaPlanes.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task PendingTransfer_BlocksTrialPlanChangeAndCancellation_UntilCentralResolvesIt()
    {
        await using var f = new BillingSecurityFixture();
        var admin = f.Service(BillingSecurityFixture.AdminA);
        (await admin.StartTrialAsync(new(BillingSecurityFixture.EmpresaA, BillingSecurityFixture.Basic, "audit@example.invalid"))).IsSuccess.Should().BeTrue();
        var expiry = (await f.Db.EmpresaPlanes.SingleAsync()).FechaFin;
        var transfer = await admin.IniciarTransferenciaAsync(new(BillingSecurityFixture.EmpresaA, BillingSecurityFixture.Basic));
        transfer.IsSuccess.Should().BeTrue();
        (await admin.ChangePlanAsync(new(BillingSecurityFixture.EmpresaA, BillingSecurityFixture.Pro))).ErrorCode.Should().Be("BILLING_TRANSFER_PENDING");
        (await admin.CancelSubscriptionAsync(new(BillingSecurityFixture.EmpresaA, false))).ErrorCode.Should().Be("BILLING_TRANSFER_PENDING");
        (await f.Db.EmpresaPlanes.SingleAsync()).FechaFin.Should().Be(expiry);
        (await f.Db.BillingSubscriptions.SingleAsync()).Status.Should().Be(SubscriptionStatus.Trialing);
        (await f.Service(BillingSecurityFixture.Central).RechazarTransferenciaAsync(transfer.Value!.PaymentId, "synthetic rejection", "forged")).IsSuccess.Should().BeTrue();
        (await admin.IniciarTransferenciaAsync(new(BillingSecurityFixture.EmpresaA, BillingSecurityFixture.Pro))).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task LegacyDuplicatePendingTransfers_MustBeResolvedBeforeAnyLicenseActivation()
    {
        await using var f = new BillingSecurityFixture();
        var payment = await f.Pending(BillingSecurityFixture.EmpresaA);
        f.Db.BillingPayments.Add(new BillingPayment { Subscription = payment.Subscription, Amount = payment.Amount,
            Currency = payment.Currency, Metodo = "TRANSFERENCIA", Status = "PENDIENTE_VERIFICACION" });
        await f.Db.SaveChangesAsync();
        (await f.Service(BillingSecurityFixture.Central).ConfirmarTransferenciaAsync(payment.Id, "forged")).ErrorCode.Should().Be("BILLING_TRANSFER_AMBIGUOUS");
        (await f.Db.EmpresaPlanes.CountAsync()).Should().Be(0);
        (await f.Db.BillingPayments.CountAsync(p => p.Status == "PENDIENTE_VERIFICACION")).Should().Be(2);
    }
}
