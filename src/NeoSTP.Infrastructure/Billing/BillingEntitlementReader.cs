using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NeoSTP.Application.Common;
using NeoSTP.Domain.Core.Billing;
using NeoSTP.Domain.Core.Licenciamiento;
using NeoSTP.Infrastructure.Persistence;

namespace NeoSTP.Infrastructure.Billing;

public sealed record BillingEntitlements(string PlanCode, string PlanName, int? LimiteUsuarios,
    int? LimiteSucursales, int? LimitePuntosVenta, int? LimiteDteMensual, int[] ModuleIds, bool FromPayment);

/// <summary>Paid rights come from the committed application, never from a later catalog edit.</summary>
public static class BillingEntitlementReader
{
    public static async Task<Result<BillingEntitlements>> ReadAsync(NeoStpDbContext db, EmpresaPlan license, CancellationToken ct = default)
    {
        // Detect adoption before filtering by matching plan/period, so replacing a paid license cannot enable legacy fallback.
        var adopted = await db.BillingPaymentApplications.AsNoTracking().AnyAsync(x =>
            x.CheckoutIntent.EmpresaId == license.EmpresaId || x.EmpresaPlanId == license.Id, ct);
        if (!adopted)
        {
            var plan = await db.Planes.AsNoTracking().SingleOrDefaultAsync(x => x.Id == license.PlanId, ct);
            if (plan is null || new[] { plan.LimiteUsuarios, plan.LimiteSucursales, plan.LimitePuntosVenta, plan.LimiteDteMensual }.Any(x => x < 0)) return Invalid();
            var modules = await db.PlanModulos.AsNoTracking().Where(x => x.PlanId == license.PlanId && x.Activo && x.Modulo.Activo)
                .OrderBy(x => x.ModuloId).Select(x => x.ModuloId).ToArrayAsync(ct);
            return Result<BillingEntitlements>.Ok(new(plan.Codigo, plan.Nombre, Normalize(plan.LimiteUsuarios),
                Normalize(plan.LimiteSucursales), Normalize(plan.LimitePuntosVenta), Normalize(plan.LimiteDteMensual), modules, false));
        }
        var application = await db.BillingPaymentApplications.AsNoTracking()
            .Include(x => x.CheckoutIntent).Include(x => x.Payment).Include(x => x.Notification)
            .Include(x => x.Subscription).ThenInclude(x => x.Customer)
            .Where(x => x.EmpresaPlanId == license.Id).OrderByDescending(x => x.Id).FirstOrDefaultAsync(ct);
        if (application is null) return Invalid();
        var intent = application.CheckoutIntent; var payment = application.Payment;
        var receipt = application.Notification; var subscription = application.Subscription;
        if (intent is null || payment is null || receipt is null || subscription?.Customer is null
            || intent.EmpresaId != license.EmpresaId || intent.PlanId != license.PlanId
            || subscription.Customer.EmpresaId != license.EmpresaId || subscription.PlanId != license.PlanId
            || subscription.Customer.Provider != "Wompi" || intent.Provider != "Wompi"
            || payment.BillingSubscriptionId != subscription.Id || payment.Status != "SUCCEEDED"
            || payment.Amount != intent.Amount || payment.Currency != intent.Currency
            || application.PeriodEnd <= application.PeriodStart || application.PeriodEnd != license.FechaFin
            || subscription.CurrentPeriodEnd != license.FechaFin
            || subscription.Status != SubscriptionStatus.Active && !(subscription.Status == SubscriptionStatus.Canceled && subscription.CancelAtPeriodEnd)
            || intent.Status != BillingCheckoutStatuses.Completed || !intent.IsProduction || !receipt.IsProduction
            || receipt.Status != BillingPaymentNotificationStatuses.VerifiedCapturedProduction || receipt.BillingCheckoutIntentId != intent.Id
            || receipt.VerifiedAt is null || receipt.ProviderPaidAt is null || payment.PaidAt != receipt.ProviderPaidAt.Value.UtcDateTime
            || receipt.CheckoutCorrelationId != intent.CorrelationId || receipt.TransactionId == Guid.Empty
            || payment.ExternalPaymentId != receipt.TransactionId.ToString("D") || receipt.Amount != intent.Amount || receipt.Currency != intent.Currency
            || receipt.ProviderAccountId != intent.ProviderAccountId || receipt.BeneficiaryId != intent.BeneficiaryId
            || receipt.ExternalCheckoutId != intent.ExternalCheckoutId || receipt.Provider != intent.Provider
            || application.CommercialSnapshotJson != intent.CommercialSnapshotJson) return Invalid();
        try
        {
            using var doc = JsonDocument.Parse(application.CommercialSnapshotJson, new JsonDocumentOptions { MaxDepth = 32 });
            using var idsDoc = JsonDocument.Parse(application.ModuleIdsJson);
            var root = doc.RootElement;
            if (Duplicates(root) || root.GetProperty("Version").GetInt32() != 1
                || root.GetProperty("EmpresaId").GetInt32() != license.EmpresaId || root.GetProperty("PlanId").GetInt32() != license.PlanId)
                return Invalid();
            var plan = root.GetProperty("Plan");
            var code = plan.GetProperty("Codigo").GetString(); var name = plan.GetProperty("Nombre").GetString();
            if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(name) || code != intent.PlanCode || name != intent.PlanName)
                return Invalid();
            var modules = root.GetProperty("Modules").EnumerateArray().Select(x => new {
                Id = x.GetProperty("ModuloId").GetInt32(), Code = x.GetProperty("Codigo").GetString() }).ToArray();
            var ids = idsDoc.RootElement.EnumerateArray().Select(x => x.GetInt32()).Order().ToArray();
            if (modules.Any(x => x.Id <= 0 || string.IsNullOrWhiteSpace(x.Code)) || modules.Select(x => x.Id).Distinct().Count() != modules.Length
                || ids.Distinct().Count() != ids.Length || !ids.SequenceEqual(modules.Select(x => x.Id).Order())) return Invalid();
            var definitions = await db.Modulos.AsNoTracking().Where(x => ids.Contains(x.Id)).Select(x => new { x.Id, x.Codigo }).ToArrayAsync(ct);
            if (definitions.Length != ids.Length || definitions.Any(x => !modules.Any(m => m.Id == x.Id && m.Code == x.Codigo))) return Invalid();
            return Result<BillingEntitlements>.Ok(new(code!, name!, Quota(plan, "LimiteUsuarios"), Quota(plan, "LimiteSucursales"),
                Quota(plan, "LimitePuntosVenta"), Quota(plan, "LimiteDteMensual"), ids, true));
        }
        catch (Exception e) when (e is JsonException or InvalidOperationException or KeyNotFoundException or FormatException or OverflowException or ArgumentException)
        { return Invalid(); }
    }

    private static int? Quota(JsonElement plan, string property)
    {
        var element = plan.GetProperty(property); // Missing is invalid, never implicitly unlimited.
        if (element.ValueKind == JsonValueKind.Null) return null;
        var value = element.GetInt32();
        if (value < 0) throw new FormatException("Invalid quota.");
        return Normalize(value);
    }
    private static int? Normalize(int? value) => value == 0 ? null : value;
    private static bool Duplicates(JsonElement node)
    {
        if (node.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in node.EnumerateObject()) if (!names.Add(property.Name) || Duplicates(property.Value)) return true;
        }
        else if (node.ValueKind == JsonValueKind.Array) foreach (var item in node.EnumerateArray()) if (Duplicates(item)) return true;
        return false;
    }
    private static Result<BillingEntitlements> Invalid() => Result<BillingEntitlements>.Fail(
        "La licencia requiere conciliar sus condiciones adquiridas.", "LICENSE_SNAPSHOT_INVALID");
}