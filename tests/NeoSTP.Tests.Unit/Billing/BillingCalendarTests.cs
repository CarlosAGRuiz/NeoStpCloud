using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NeoSTP.Application.Billing.Dtos;
using NeoSTP.Domain.Core.Billing;
using NeoSTP.Domain.Core.Licenciamiento;
using NeoSTP.Infrastructure.Billing;

namespace NeoSTP.Tests.Unit.Billing;

public class BillingCalendarTests
{
    [Theory]
    [InlineData(2026, 9, 30)] [InlineData(2026, 10, 31)] [InlineData(2026, 11, 30)]
    [InlineData(2027, 2, 28)] [InlineData(2028, 2, 29)]
    public void MonthEndIsLocalAndUtcBoundaryIsSeparate(int year, int month, int days)
    {
        var start = new DateOnly(year, month, 1);
        BillingCalendar.DueDate(start).Should().Be(new DateOnly(year, month, days));
        BillingCalendar.StartUtc(start).Should().Be(new DateTime(year, month, 1, 6, 0, 0, DateTimeKind.Utc));
        BillingCalendar.MonthAt(BillingCalendar.StartUtc(start).AddTicks(-1)).Should().Be(start.AddMonths(-1));
    }

    private static async Task<BillingCalendarAgreement> Seed(BillingSecurityFixture f)
    {
        var sub = new BillingSubscription { Customer = new() { EmpresaId = BillingSecurityFixture.EmpresaA, Provider = "Mock", Email = "test@example.invalid" },
            PlanId = BillingSecurityFixture.Basic, Status = "ACTIVE", TrialStart = new DateTime(2026, 8, 20), TrialEnd = new DateTime(2026, 9, 3) };
        var license = new EmpresaPlan { EmpresaId = BillingSecurityFixture.EmpresaA, PlanId = BillingSecurityFixture.Basic,
            FechaInicio = new DateTime(2026, 8, 20), FechaFin = null, EstadoCodigo = "ACTIVO" };
        f.Db.BillingSubscriptions.Add(sub); f.Db.EmpresaPlanes.Add(license); await f.Db.SaveChangesAsync();
        var agreement = new BillingCalendarAgreement { EmpresaId = BillingSecurityFixture.EmpresaA, BillingSubscriptionId = sub.Id, Subscription = sub,
            EmpresaPlanId = license.Id, PlanId = license.PlanId, MonthlyAmount = 10, FirstPeriodStartLocal = new DateOnly(2026, 9, 1),
            AgreementKey = "SYNTHETIC-CALENDAR", Reason = "Synthetic agreement" };
        f.Db.BillingCalendarAgreements.Add(agreement); await f.Db.SaveChangesAsync(); return agreement;
    }

    [Fact]
    public async Task CycleIsIdempotentPreservesAccessAndDoesNotCreatePaymentsOrAugustDebt()
    {
        await using var f = new BillingSecurityFixture(); var agreement = await Seed(f);
        var now = new DateTime(2026, 11, 9, 15, 0, 0, DateTimeKind.Utc);
        (await BillingCalendarCycle.AdvanceAsync(f.Db, agreement.EmpresaId, now)).IsSuccess.Should().BeTrue();
        (await BillingCalendarCycle.AdvanceAsync(f.Db, agreement.EmpresaId, now)).IsSuccess.Should().BeTrue();
        var periods = await f.Db.BillingCalendarPeriods.OrderBy(x => x.PeriodStartLocal).ToListAsync();
        periods.Should().HaveCount(3);
        periods.Select(x => x.DueLocalDate).Should().Equal(new DateOnly(2026, 9, 30), new DateOnly(2026, 10, 31), new DateOnly(2026, 11, 30));
        (await f.Db.BillingInvoices.ToListAsync()).Should().OnlyContain(x => x.Status == "OPEN" && x.PaidAt == null && x.Amount == 10);
        (await f.Db.BillingPayments.CountAsync()).Should().Be(0);
        (await f.Db.EmpresaPlanes.SingleAsync()).FechaFin.Should().BeNull();
        agreement.Subscription.TrialEnd.Should().Be(new DateTime(2026, 9, 3));
    }

    [Fact]
    public async Task ExplicitRevocationStopsCycleAndDoesNotReactivateLicense()
    {
        await using var f = new BillingSecurityFixture(); var agreement = await Seed(f);
        var license = await f.Db.EmpresaPlanes.SingleAsync(); license.EstadoCodigo = "CANCELADO"; await f.Db.SaveChangesAsync();
        (await BillingCalendarCycle.AdvanceAsync(f.Db, agreement.EmpresaId, DateTime.UtcNow)).IsSuccess.Should().BeTrue();
        (await f.Db.BillingInvoices.CountAsync()).Should().Be(0); license.EstadoCodigo.Should().Be("CANCELADO");
    }

    [Fact]
    public async Task ForeignCompanyCycleDoesNotTouchAgreement()
    {
        await using var f = new BillingSecurityFixture(); await Seed(f);
        (await BillingCalendarCycle.AdvanceAsync(f.Db, BillingSecurityFixture.EmpresaB, DateTime.UtcNow)).IsSuccess.Should().BeTrue();
        (await f.Db.BillingInvoices.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task LegacyTransferCheckoutAndPlanChangesAreBlocked()
    {
        await using var f = new BillingSecurityFixture(); await Seed(f); var service = f.Service(BillingSecurityFixture.AdminA);
        (await service.IniciarTransferenciaAsync(new(BillingSecurityFixture.EmpresaA, BillingSecurityFixture.Basic))).ErrorCode.Should().Be("BILLING_CALENDAR_MANAGED");
        (await service.ChangePlanAsync(new(BillingSecurityFixture.EmpresaA, BillingSecurityFixture.Pro))).ErrorCode.Should().Be("BILLING_CALENDAR_MANAGED");
        (await service.CreateCheckoutSessionAsync(new(BillingSecurityFixture.EmpresaA, BillingSecurityFixture.Pro, "/billing", "Wompi", "calendar-test-001"))).ErrorCode.Should().Be("BILLING_CALENDAR_MANAGED");
        (await f.Db.BillingPayments.CountAsync()).Should().Be(0); (await f.Db.BillingCheckoutIntents.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task RealPaymentIsIdempotentDoesNotMoveCalendarAndRejectsForeignTenant()
    {
        await using var f = new BillingSecurityFixture(); var agreement = await Seed(f);
        await BillingCalendarCycle.AdvanceAsync(f.Db, agreement.EmpresaId, new DateTime(2026, 9, 6, 15, 0, 0, DateTimeKind.Utc));
        var period = await f.Db.BillingCalendarPeriods.SingleAsync();
        var service = new BillingCalendarService(f.Db, f.Identity(BillingSecurityFixture.Central));
        var paid = DateTime.UtcNow.AddHours(-2);
        (await service.ApplyVerifiedPaymentAsync(BillingSecurityFixture.EmpresaB, period.Id, 10, "USD", "BANK-123", paid)).ErrorCode.Should().Be("BILLING_PERIOD_NOT_FOUND");
        (await service.ApplyVerifiedPaymentAsync(agreement.EmpresaId, period.Id, 10, "USD", "BANK-123", paid)).IsSuccess.Should().BeTrue();
        (await service.ApplyVerifiedPaymentAsync(agreement.EmpresaId, period.Id, 10, "USD", "BANK-123", paid)).IsSuccess.Should().BeTrue();
        (await f.Db.BillingPayments.CountAsync()).Should().Be(1);
        period.Invoice.Status.Should().Be("PAID"); period.DueLocalDate.Should().Be(new DateOnly(2026, 9, 30));
        (await f.Db.EmpresaPlanes.SingleAsync()).FechaFin.Should().BeNull();
    }

    [Fact]
    public async Task TenantAdminCannotConfirmAndInvoiceDriftCannotCrossSubscription()
    {
        await using var f = new BillingSecurityFixture(); var agreement = await Seed(f);
        await BillingCalendarCycle.AdvanceAsync(f.Db, agreement.EmpresaId, new DateTime(2026, 9, 6, 15, 0, 0, DateTimeKind.Utc));
        var period = await f.Db.BillingCalendarPeriods.SingleAsync();
        (await new BillingCalendarService(f.Db, f.Identity(BillingSecurityFixture.AdminA))
            .ApplyVerifiedPaymentAsync(agreement.EmpresaId, period.Id, 10, "USD", "BANK-123", DateTime.UtcNow.AddHours(-1))).ErrorCode.Should().Be("BILLING_FORBIDDEN");
        period.Invoice.BillingSubscriptionId += 100;
        (await new BillingCalendarService(f.Db, f.Identity(BillingSecurityFixture.Central))
            .ApplyVerifiedPaymentAsync(agreement.EmpresaId, period.Id, 10, "USD", "BANK-123", DateTime.UtcNow.AddHours(-1))).ErrorCode.Should().Be("BILLING_PERIOD_NOT_FOUND");
        (await f.Db.BillingPayments.CountAsync()).Should().Be(0);
    }
}
