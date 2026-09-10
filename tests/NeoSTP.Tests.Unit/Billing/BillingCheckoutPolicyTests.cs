using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NeoSTP.Domain.Core.Billing;
using NeoSTP.Domain.Core.Licenciamiento;
using NeoSTP.Infrastructure.Billing;

namespace NeoSTP.Tests.Unit.Billing;

public sealed class BillingCheckoutPolicyTests
{
    [Fact]
    public async Task Clean_first_purchase_is_supported_without_mutating_billing()
    {
        await using var f = new BillingEntitlementFixture();
        var result = await BillingCheckoutPolicy.ValidateAsync(f.Db, f.Intent, f.PaidAt);
        result.IsSuccess.Should().BeTrue(result.Error);
        f.Db.BillingCustomers.Should().BeEmpty(); f.Db.BillingPayments.Should().BeEmpty();
        f.Db.BillingPaymentApplications.Should().BeEmpty(); f.Db.EmpresaPlanes.Should().BeEmpty();
    }

    [Fact]
    public async Task Paid_same_plan_same_terms_renewal_is_supported_without_mutation()
    {
        await using var f = new BillingEntitlementFixture(); await f.Paid();
        var intent = await f.Renewal();
        var previousEnd = (await f.Db.EmpresaPlanes.SingleAsync()).FechaFin;
        var result = await BillingCheckoutPolicy.ValidateAsync(f.Db, intent, DateTime.UtcNow);
        result.IsSuccess.Should().BeTrue(result.Error);
        f.Db.BillingPayments.Should().ContainSingle(); f.Db.BillingPaymentApplications.Should().ContainSingle();
        (await f.Db.EmpresaPlanes.SingleAsync()).FechaFin.Should().Be(previousEnd);
    }

    [Fact]
    public async Task Current_local_Wompi_trial_is_supported()
    {
        await using var f = new BillingEntitlementFixture();
        var customer = new BillingCustomer { EmpresaId = BillingSecurityFixture.EmpresaA, Provider = "Wompi", Email = "synthetic@example.invalid" };
        var trialEnd = DateTime.UtcNow.AddDays(5);
        var sub = new BillingSubscription { Customer = customer, PlanId = BillingSecurityFixture.Basic,
            Status = SubscriptionStatus.Trialing, TrialStart = DateTime.UtcNow.AddDays(-3), TrialEnd = trialEnd };
        var license = new EmpresaPlan { EmpresaId = BillingSecurityFixture.EmpresaA, PlanId = BillingSecurityFixture.Basic,
            EstadoCodigo = "ACTIVO", FechaInicio = sub.TrialStart, FechaFin = trialEnd };
        f.Db.BillingSubscriptions.Add(sub); f.Db.EmpresaPlanes.Add(license); await f.Db.SaveChangesAsync();
        f.Intent.BillingCustomerId = customer.Id; f.Intent.BillingSubscriptionId = sub.Id; f.Intent.EmpresaPlanId = license.Id;
        var result = await BillingCheckoutPolicy.ValidateAsync(f.Db, f.Intent, DateTime.UtcNow);
        result.IsSuccess.Should().BeTrue(result.Error);
    }

    [Theory]
    [InlineData("quota-users")]
    [InlineData("quota-branches")]
    [InlineData("quota-points")]
    [InlineData("quota-dte")]
    [InlineData("added-module")]
    [InlineData("removed-module")]
    [InlineData("revoked-company-module")]
    [InlineData("active-with-inactivation-date")]
    [InlineData("global-module-disabled")]
    [InlineData("addon")]
    [InlineData("invalid-ledger")]
    public async Task Paid_renewal_with_changed_rights_or_operational_revocation_is_rejected(string change)
    {
        await using var f = new BillingEntitlementFixture(); await f.Paid();
        var intent = await f.Renewal();
        var plan = (await f.Db.Planes.FindAsync(BillingSecurityFixture.Basic))!;
        switch (change)
        {
            case "quota-users": plan.LimiteUsuarios = 6; break;
            case "quota-branches": plan.LimiteSucursales = 3; break;
            case "quota-points": plan.LimitePuntosVenta = 4; break;
            case "quota-dte": plan.LimiteDteMensual = 101; break;
            case "added-module": f.Db.PlanModulos.Add(new() { PlanId = BillingSecurityFixture.Basic, ModuloId = BillingEntitlementFixture.ExtraModule }); break;
            case "removed-module": (await f.Db.PlanModulos.SingleAsync()).Activo = false; break;
            case "revoked-company-module": (await f.Db.EmpresaModulos.SingleAsync()).Activo = false; break;
            case "active-with-inactivation-date": (await f.Db.EmpresaModulos.SingleAsync()).FechaInactivacion = DateTime.UtcNow.AddMinutes(-1); break;
            case "global-module-disabled": (await f.Db.Modulos.FindAsync(BillingEntitlementFixture.Module))!.Activo = false; break;
            case "addon": f.Db.EmpresaModulos.Add(new() { EmpresaId = BillingSecurityFixture.EmpresaA, ModuloId = BillingEntitlementFixture.ExtraModule, Activo = true }); break;
            case "invalid-ledger": (await f.Db.BillingPaymentApplications.SingleAsync()).ModuleIdsJson = "[]"; break;
        }
        await f.Db.SaveChangesAsync();
        var result = await BillingCheckoutPolicy.ValidateAsync(f.Db, intent, DateTime.UtcNow);
        result.ErrorCode.Should().Be("BILLING_TRANSITION_UNSUPPORTED");
        f.Db.BillingPayments.Should().ContainSingle();
        if (change == "revoked-company-module") (await f.Db.EmpresaModulos.SingleAsync()).Activo.Should().BeFalse();
        if (change == "active-with-inactivation-date") (await f.Db.EmpresaModulos.SingleAsync()).FechaInactivacion.Should().NotBeNull();
    }

    [Theory]
    [InlineData("canceled")]
    [InlineData("suspended")]
    [InlineData("cancel-at-end")]
    [InlineData("canceled-at")]
    [InlineData("external-subscription")]
    [InlineData("plan-change")]
    [InlineData("license-replaced")]
    [InlineData("duplicate-history")]
    public async Task Unsupported_paid_transition_is_blocked_before_a_new_checkout(string scenario)
    {
        await using var f = new BillingEntitlementFixture(); await f.Paid();
        var intent = await f.Renewal();
        var sub = await f.Db.BillingSubscriptions.SingleAsync();
        switch (scenario)
        {
            case "canceled": sub.Status = SubscriptionStatus.Canceled; break;
            case "suspended": sub.Status = SubscriptionStatus.Suspended; break;
            case "cancel-at-end": sub.CancelAtPeriodEnd = true; break;
            case "canceled-at": sub.CanceledAt = DateTime.UtcNow; break;
            case "external-subscription": sub.ExternalSubscriptionId = "synthetic-external-sub"; break;
            case "plan-change": intent.PlanId = BillingSecurityFixture.Pro; intent.Amount = 50; break;
            case "license-replaced": intent.EmpresaPlanId = 999999; break;
            case "duplicate-history": f.Db.BillingSubscriptions.Add(new() { BillingCustomerId = sub.BillingCustomerId,
                PlanId = BillingSecurityFixture.Basic, Status = SubscriptionStatus.Canceled }); break;
        }
        await f.Db.SaveChangesAsync();
        var result = await BillingCheckoutPolicy.ValidateAsync(f.Db, intent, DateTime.UtcNow);
        result.ErrorCode.Should().Be("BILLING_TRANSITION_UNSUPPORTED");
        f.Db.BillingPayments.Should().ContainSingle(); f.Db.BillingPaymentApplications.Should().ContainSingle();
    }

    [Theory]
    [InlineData("negative-quota")]
    [InlineData("inactive-company")]
    [InlineData("inactive-plan")]
    [InlineData("foreign-provider")]
    [InlineData("year")]
    [InlineData("addon")]
    public async Task Invalid_first_purchase_terms_are_rejected_without_creating_billing_data(string scenario)
    {
        await using var f = new BillingEntitlementFixture();
        switch (scenario)
        {
            case "negative-quota": (await f.Db.Planes.FindAsync(BillingSecurityFixture.Basic))!.LimiteUsuarios = -1; break;
            case "inactive-company": (await f.Db.Empresas.FindAsync(BillingSecurityFixture.EmpresaA))!.EstadoCodigo = "SUSPENDIDA"; break;
            case "inactive-plan": (await f.Db.Planes.FindAsync(BillingSecurityFixture.Basic))!.Activo = false; break;
            case "foreign-provider": f.Intent.Provider = "Mock"; break;
            case "year": f.Intent.BillingInterval = "YEAR"; break;
            case "addon": f.Db.EmpresaModulos.Add(new() { EmpresaId = BillingSecurityFixture.EmpresaA, ModuloId = BillingEntitlementFixture.ExtraModule }); break;
        }
        await f.Db.SaveChangesAsync();
        var result = await BillingCheckoutPolicy.ValidateAsync(f.Db, f.Intent, f.PaidAt);
        result.ErrorCode.Should().Be("BILLING_TRANSITION_UNSUPPORTED");
        f.Db.BillingPayments.Should().BeEmpty(); f.Db.EmpresaPlanes.Should().BeEmpty();
    }
}
