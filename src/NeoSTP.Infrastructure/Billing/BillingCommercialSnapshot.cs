using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NeoSTP.Domain.Core.Billing;
using NeoSTP.Infrastructure.Persistence;

namespace NeoSTP.Infrastructure.Billing;

/// <summary>Versioned pre-checkout state; no customer contacts or credentials.</summary>
public static class BillingCommercialSnapshot
{
    public static async Task<string> CaptureAsync(NeoStpDbContext db, BillingCheckoutIntent intent, CancellationToken ct = default)
    {
        var plan = await db.Planes.AsNoTracking().SingleAsync(x => x.Id == intent.PlanId, ct);
        var modules = await db.PlanModulos.AsNoTracking().Where(x => x.PlanId == plan.Id && x.Activo && x.Modulo.Activo)
            .OrderBy(x => x.ModuloId).Select(x => new { x.ModuloId, x.Modulo.Codigo }).ToArrayAsync(ct);
        var customers = await db.BillingCustomers.AsNoTracking().Where(x => x.EmpresaId == intent.EmpresaId)
            .OrderBy(x => x.Id).Select(x => new { x.Id, x.Provider }).ToArrayAsync(ct);
        var subscriptions = await db.BillingSubscriptions.AsNoTracking().Where(x => x.Customer.EmpresaId == intent.EmpresaId)
            .OrderBy(x => x.Id).ToArrayAsync(ct);
        var licenses = await db.EmpresaPlanes.AsNoTracking().Where(x => x.EmpresaId == intent.EmpresaId)
            .OrderBy(x => x.Id).ToArrayAsync(ct);
        var companyModules = await db.EmpresaModulos.AsNoTracking().Where(x => x.EmpresaId == intent.EmpresaId)
            .OrderBy(x => x.ModuloId).Select(x => new { x.ModuloId, x.Activo, x.FechaInactivacion,
                x.ComplementoAutorizado, x.ComplementoAutorizadoAt, x.ComplementoAutorizadoBy, x.ComplementoMotivo }).ToArrayAsync(ct);
        return JsonSerializer.Serialize(new
        {
            Version = 1, intent.EmpresaId, intent.PlanId,
            Plan = new { plan.Codigo, plan.Nombre, Price = plan.PrecioMensual.ToString("F2", CultureInfo.InvariantCulture),
                plan.MonedaCodigo, plan.Activo, plan.LimiteUsuarios, plan.LimiteSucursales, plan.LimitePuntosVenta, plan.LimiteDteMensual },
            Modules = modules, Customers = customers,
            Subscriptions = subscriptions.Select(x => new { x.Id, x.BillingCustomerId, x.PlanId, x.Status, x.ExternalSubscriptionId,
                TrialStart = Utc(x.TrialStart), TrialEnd = Utc(x.TrialEnd), Start = Utc(x.CurrentPeriodStart), End = Utc(x.CurrentPeriodEnd),
                CanceledAt = Utc(x.CanceledAt), x.CancelAtPeriodEnd }),
            Licenses = licenses.Select(x => new { x.Id, x.PlanId, x.EstadoCodigo, Start = Utc(x.FechaInicio), End = Utc(x.FechaFin) }),
            CompanyModules = companyModules,
        });
    }

    private static string? Utc(DateTime? value) => value.HasValue
        ? DateTime.SpecifyKind(value.Value, DateTimeKind.Utc).ToString("O", CultureInfo.InvariantCulture) : null;
}
