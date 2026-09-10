using Microsoft.EntityFrameworkCore;
using NeoSTP.Application.Common;
using NeoSTP.Domain.Core.Billing;
using NeoSTP.Infrastructure.Persistence;

namespace NeoSTP.Infrastructure.Billing;

/// <summary>Same supported-transition gate before creating an external link and before applying its capture.</summary>
public static class BillingCheckoutPolicy
{
    public static async Task<Result> ValidateAsync(NeoStpDbContext db, BillingCheckoutIntent intent, DateTime effectiveAt, CancellationToken ct = default)
    {
        var company = await db.Empresas.AsNoTracking().SingleOrDefaultAsync(x => x.Id == intent.EmpresaId, ct);
        var plan = await db.Planes.AsNoTracking().SingleOrDefaultAsync(x => x.Id == intent.PlanId, ct);
        if (company?.EstadoCodigo != "ACTIVA" || plan is null || !plan.Activo || intent.Provider != "Wompi"
            || intent.BillingInterval != "MONTH" || intent.Currency != "USD" || plan.MonedaCodigo != intent.Currency
            || plan.PrecioMensual != intent.Amount || intent.Amount <= 0
            || new[] { plan.LimiteUsuarios, plan.LimiteSucursales, plan.LimitePuntosVenta, plan.LimiteDteMensual }.Any(x => x < 0)) return Unsupported();
        var customers = await db.BillingCustomers.AsNoTracking().Where(x => x.EmpresaId == intent.EmpresaId).ToListAsync(ct);
        var subscriptions = await db.BillingSubscriptions.AsNoTracking().Where(x => x.Customer.EmpresaId == intent.EmpresaId).ToListAsync(ct);
        var licenses = await db.EmpresaPlanes.AsNoTracking().Where(x => x.EmpresaId == intent.EmpresaId).ToListAsync(ct);
        if (customers.Count > 1 || subscriptions.Count > 1 || licenses.Count > 1 || customers.Any(x => x.Provider != intent.Provider)) return Unsupported();
        var customer = customers.SingleOrDefault(); var subscription = subscriptions.SingleOrDefault(); var license = licenses.SingleOrDefault();
        if (subscription is null)
        {
            if (intent.BillingSubscriptionId is not null || intent.EmpresaPlanId is not null || license is not null
                || intent.BillingCustomerId is not null && intent.BillingCustomerId != customer?.Id
                || await db.BillingPaymentApplications.AnyAsync(x => x.CheckoutIntent.EmpresaId == intent.EmpresaId, ct)) return Unsupported();
        }
        else if (customer is null || intent.BillingCustomerId != customer.Id || subscription.BillingCustomerId != customer.Id
            || subscription.Id != intent.BillingSubscriptionId || subscription.PlanId != intent.PlanId
            || subscription.Status is not (SubscriptionStatus.Active or SubscriptionStatus.Trialing)
            || subscription.CanceledAt is not null || subscription.CancelAtPeriodEnd || subscription.ExternalSubscriptionId is not null
            || license is null || license.Id != intent.EmpresaPlanId || license.PlanId != intent.PlanId || license.EstadoCodigo != "ACTIVO"
            || license.FechaFin != (subscription.Status == SubscriptionStatus.Active ? subscription.CurrentPeriodEnd : subscription.TrialEnd)
            || subscription.Status == SubscriptionStatus.Active && subscription.CurrentPeriodEnd is null
            || subscription.Status == SubscriptionStatus.Trialing && subscription.TrialEnd < effectiveAt) return Unsupported();
        var moduleIds = await db.PlanModulos.AsNoTracking().Where(x => x.PlanId == intent.PlanId && x.Activo && x.Modulo.Activo)
            .OrderBy(x => x.ModuloId).Select(x => x.ModuloId).ToArrayAsync(ct);
        var companyModules = await db.EmpresaModulos.AsNoTracking().Where(x => x.EmpresaId == intent.EmpresaId).ToListAsync(ct);
        if (companyModules.Any(x => x.Activo && !moduleIds.Contains(x.ModuloId) && !EmpresaModuloEntitlements.HasGrant(x))) return Unsupported();
        if (license is not null)
        {
            var current = await BillingEntitlementReader.ReadAsync(db, license, ct);
            if (current.IsFailure) return Unsupported();
            var terms = current.Value!;
            if (terms.FromPayment && (terms.LimiteUsuarios != Normalize(plan.LimiteUsuarios)
                || terms.LimiteSucursales != Normalize(plan.LimiteSucursales) || terms.LimitePuntosVenta != Normalize(plan.LimitePuntosVenta)
                || terms.LimiteDteMensual != Normalize(plan.LimiteDteMensual) || !terms.ModuleIds.Order().SequenceEqual(moduleIds)
                || moduleIds.Any(id => !companyModules.Any(x => x.ModuloId == id && x.Activo && x.FechaInactivacion == null)))) return Unsupported();
        }
        return Result.Ok();
    }
    private static int? Normalize(int? value) => value == 0 ? null : value;
    private static Result Unsupported() => Result.Fail("Esta transición de plan, cuotas o módulos requiere revisión antes de iniciar el pago.", "BILLING_TRANSITION_UNSUPPORTED");
}
