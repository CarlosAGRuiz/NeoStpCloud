using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Options;
using NeoSTP.Application.Billing;
using NeoSTP.Application.Common;
using NeoSTP.Domain.Core.Billing;
using NeoSTP.Domain.Core.Licenciamiento;
using NeoSTP.Infrastructure.Persistence;

namespace NeoSTP.Infrastructure.Billing;

/// <summary>Internal atomic application of durable captured evidence. No provider, email or network calls.</summary>
public sealed class BillingPaymentApplicationProcessor(NeoStpDbContext db, IOptions<BillingOptions> options)
    : IBillingPaymentApplicationProcessor
{
    public async Task<Result> ApplyVerifiedPaymentAsync(Guid receiptId, CancellationToken ct = default)
    {
        if (options.Value.PaymentApplication?.Enabled != true) return Fail("BILLING_APPLICATION_DISABLED");
        // This scoped processor owns its unit of work, including rollback recovery.
        db.ChangeTracker.Clear();
        try { return await new SingleAttempt(db).ExecuteAsync(() => ApplyAsync(receiptId, ct)); }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { db.ChangeTracker.Clear(); throw; }
        catch (Exception) { db.ChangeTracker.Clear(); return Fail("BILLING_APPLICATION_RETRY_REQUIRED"); }
    }

    private async Task<Result> ApplyAsync(Guid receiptId, CancellationToken ct)
    {
        var companyId = await (from notification in db.BillingPaymentNotifications.AsNoTracking()
            join checkout in db.BillingCheckoutIntents.AsNoTracking() on notification.BillingCheckoutIntentId equals (int?)checkout.Id
            where notification.ReceiptId == receiptId select (int?)checkout.EmpresaId).SingleOrDefaultAsync(ct);
        if (companyId is not int empresaId) return Fail("BILLING_CAPTURE_NOT_VERIFIED");
        await using var transaction = await LockAsync(empresaId, ct);
        var receipt = await db.BillingPaymentNotifications.SingleAsync(x => x.ReceiptId == receiptId, ct);
        var intent = receipt.BillingCheckoutIntentId is int intentId
            ? await db.BillingCheckoutIntents.SingleOrDefaultAsync(x => x.Id == intentId, ct) : null;
        if (intent is null || intent.EmpresaId != empresaId || !Matches(receipt, intent))
            return Fail("BILLING_CAPTURE_NOT_VERIFIED");
        // Replay precedes state/snapshot checks: cancellation after a committed payment must stay canceled.
        var applied = await db.BillingPaymentApplications.AsNoTracking().SingleOrDefaultAsync(x => x.BillingCheckoutIntentId == intent.Id, ct);
        if (applied is not null)
            return applied.BillingPaymentNotificationId == receipt.Id ? Result.Ok() : Fail("BILLING_ADDITIONAL_PAYMENT_RECONCILIATION");
        if (intent.Status is not (BillingCheckoutStatuses.Processing or BillingCheckoutStatuses.AwaitingPayment
            or BillingCheckoutStatuses.RequiresReconciliation)) return Fail("BILLING_APPLICATION_STATE_CONFLICT");
        if (await db.BillingProviderOperations.AnyAsync(x => x.EmpresaId == empresaId && x.Status != BillingProviderOperationStatuses.Completed, ct)
            || await db.BillingCheckoutIntents.AnyAsync(x => x.EmpresaId == empresaId && x.Id != intent.Id && x.Status != BillingCheckoutStatuses.Completed, ct)
            || await db.BillingPayments.AnyAsync(x => x.Subscription.Customer.EmpresaId == empresaId
                && x.Status == "PENDIENTE_VERIFICACION", ct)) return Fail("BILLING_APPLICATION_CONFLICT");
        if (string.IsNullOrWhiteSpace(intent.CommercialSnapshotJson)
            || intent.CommercialSnapshotJson != await BillingCommercialSnapshot.CaptureAsync(db, intent, ct))
            return Fail("BILLING_COMMERCIAL_SNAPSHOT_CHANGED");
        var policy = await BillingCheckoutPolicy.ValidateAsync(db, intent, receipt.ProviderPaidAt!.Value.UtcDateTime, ct);
        if (policy.IsFailure) return policy;
        var company = await db.Empresas.AsNoTracking().SingleAsync(x => x.Id == empresaId, ct);
        var plan = await db.Planes.AsNoTracking().SingleAsync(x => x.Id == intent.PlanId, ct);
        if (company.EstadoCodigo != "ACTIVA" || !plan.Activo || plan.PrecioMensual != intent.Amount
            || plan.MonedaCodigo != intent.Currency || plan.Codigo != intent.PlanCode || plan.Nombre != intent.PlanName)
            return Fail("BILLING_COMMERCIAL_SNAPSHOT_CHANGED");
        var customers = await db.BillingCustomers.Where(x => x.EmpresaId == empresaId).ToListAsync(ct);
        var subscriptions = await db.BillingSubscriptions.Where(x => x.Customer.EmpresaId == empresaId).ToListAsync(ct);
        var licenses = await db.EmpresaPlanes.Where(x => x.EmpresaId == empresaId).ToListAsync(ct);
        if (customers.Count > 1 || subscriptions.Count > 1 || licenses.Count > 1
            || customers.Any(x => x.Provider != intent.Provider)) return Fail("BILLING_TRANSITION_UNSUPPORTED");
        var customer = customers.SingleOrDefault();
        var subscription = subscriptions.SingleOrDefault();
        var license = licenses.SingleOrDefault();
        if (subscription is null)
        {
            if (intent.BillingSubscriptionId is not null || intent.EmpresaPlanId is not null || license is not null
                || intent.BillingCustomerId is not null && intent.BillingCustomerId != customer?.Id)
                return Fail("BILLING_TRANSITION_UNSUPPORTED");
        }
        else if (customer is null || subscription.Id != intent.BillingSubscriptionId || customer.Id != intent.BillingCustomerId
            || subscription.BillingCustomerId != customer.Id || subscription.PlanId != intent.PlanId
            || subscription.Status is not (SubscriptionStatus.Active or SubscriptionStatus.Trialing)
            || subscription.CanceledAt is not null || subscription.CancelAtPeriodEnd || subscription.ExternalSubscriptionId is not null
            || license is null || license.Id != intent.EmpresaPlanId || license.PlanId != intent.PlanId || license.EstadoCodigo != "ACTIVO"
            || license.FechaFin != (subscription.Status == SubscriptionStatus.Active ? subscription.CurrentPeriodEnd : subscription.TrialEnd)
            || subscription.Status == SubscriptionStatus.Active && subscription.CurrentPeriodEnd is null
            || subscription.Status == SubscriptionStatus.Trialing && subscription.TrialEnd < receipt.ProviderPaidAt!.Value.UtcDateTime)
            return Fail("BILLING_TRANSITION_UNSUPPORTED");
        var moduleIds = await db.PlanModulos.Where(x => x.PlanId == intent.PlanId && x.Activo && x.Modulo.Activo)
            .OrderBy(x => x.ModuloId).Select(x => x.ModuloId).ToArrayAsync(ct);
        var companyModules = await db.EmpresaModulos.Where(x => x.EmpresaId == empresaId).ToListAsync(ct);
        // Explicit company grants survive renewal; unexplained out-of-plan activations still block adoption.
        if (companyModules.Any(x => x.Activo && !moduleIds.Contains(x.ModuloId) && !EmpresaModuloEntitlements.HasGrant(x))) return Fail("BILLING_TRANSITION_UNSUPPORTED");
        var paidAt = receipt.ProviderPaidAt!.Value.UtcDateTime;
        var periodStart = subscription?.Status == SubscriptionStatus.Active && subscription.CurrentPeriodEnd > paidAt
            ? subscription.CurrentPeriodEnd.Value : paidAt;
        var periodEnd = periodStart.AddMonths(1);
        var now = DateTime.UtcNow;
        if (customer is null)
        {
            customer = new BillingCustomer { EmpresaId = empresaId, Provider = intent.Provider, Email = company.Correo ?? string.Empty };
            db.BillingCustomers.Add(customer);
        }
        if (subscription is null)
        {
            subscription = new BillingSubscription { Customer = customer, PlanId = intent.PlanId, TrialStart = paidAt, TrialEnd = paidAt };
            db.BillingSubscriptions.Add(subscription);
        }
        subscription.Status = SubscriptionStatus.Active;
        subscription.CurrentPeriodStart = periodStart; subscription.CurrentPeriodEnd = periodEnd;
        subscription.UpdatedAt = now;
        if (license is null)
        {
            license = new EmpresaPlan { EmpresaId = empresaId, PlanId = intent.PlanId, FechaInicio = periodStart };
            db.EmpresaPlanes.Add(license);
        }
        license.EstadoCodigo = "ACTIVO"; license.FechaFin = periodEnd; license.UpdatedAt = now;
        foreach (var moduleId in moduleIds)
        {
            var module = companyModules.SingleOrDefault(x => x.ModuloId == moduleId);
            if (module is null) db.EmpresaModulos.Add(new EmpresaModulo { EmpresaId = empresaId, ModuloId = moduleId, FechaActivacion = now });
            else if (!module.Activo) { module.Activo = true; module.FechaActivacion = now; module.FechaInactivacion = null; }
        }
        var payment = new BillingPayment { Subscription = subscription, ExternalPaymentId = receipt.TransactionId.ToString("D"),
            Amount = intent.Amount, Currency = intent.Currency, Status = "SUCCEEDED", Metodo = "WOMPI", PaidAt = paidAt };
        db.BillingPayments.Add(payment);
        db.BillingPaymentApplications.Add(new BillingPaymentApplication { CheckoutIntent = intent, Notification = receipt,
            Payment = payment, Subscription = subscription, EmpresaPlan = license,
            PeriodStart = periodStart, PeriodEnd = periodEnd, AppliedAt = now, ModuleIdsJson = JsonSerializer.Serialize(moduleIds),
            CommercialSnapshotJson = intent.CommercialSnapshotJson });
        // Preserve pre-checkout IDs/snapshot on the intent; resulting identities live in the application ledger.
        intent.Status = BillingCheckoutStatuses.Completed; intent.CompletedAt = now; intent.LastErrorCode = null; intent.UpdatedAt = now;
        await db.SaveChangesAsync(ct);
        if (transaction is not null) await transaction.CommitAsync(ct);
        return Result.Ok();
    }

    private static bool Matches(BillingPaymentNotification r, BillingCheckoutIntent i)
        => r.Status == BillingPaymentNotificationStatuses.VerifiedCapturedProduction && r.IsProduction && i.IsProduction
            && r.VerifiedAt is not null && r.VerifiedAt <= DateTime.UtcNow.AddMinutes(5)
            && r.ProviderPaidAt is not null && r.ProviderPaidAt <= DateTimeOffset.UtcNow.AddMinutes(5)
            && r.ProviderPaidAt.Value.UtcDateTime >= i.CreatedAt.AddMinutes(-5)
            && (r.TransactionAt - r.ProviderPaidAt.Value).Duration() <= TimeSpan.FromMinutes(1)
            && r.TransactionId != Guid.Empty && r.CheckoutCorrelationId == i.CorrelationId && i.CorrelationId != Guid.Empty
            && i.Provider == "Wompi" && r.Provider == i.Provider && r.ProviderAccountId == i.ProviderAccountId
            && !string.IsNullOrWhiteSpace(i.ProviderAccountId) && r.BeneficiaryId == i.BeneficiaryId && !string.IsNullOrWhiteSpace(i.BeneficiaryId)
            && r.ExternalCheckoutId == i.ExternalCheckoutId && !string.IsNullOrWhiteSpace(i.ExternalCheckoutId)
            && r.Amount == i.Amount && i.Amount > 0 && decimal.Round(i.Amount, 2) == i.Amount
            && r.Currency == i.Currency && i.Currency == "USD" && i.BillingInterval == "MONTH";

    private sealed class SingleAttempt(DbContext context) : ExecutionStrategy(context, 0, TimeSpan.Zero)
    { protected override bool ShouldRetryOn(Exception exception) => false; }
    private async Task<IDbContextTransaction?> LockAsync(int empresaId, CancellationToken ct)
    {
        if (!db.Database.IsRelational()) return null;
        var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            if (db.Database.IsSqlServer())
                await db.Database.ExecuteSqlInterpolatedAsync($"""
                    DECLARE @r int;
                    EXEC @r = sys.sp_getapplock @Resource = {$"NeoSTP:BILLING:{empresaId}"}, @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 15000;
                    IF @r < 0 THROW 50001, 'Payment application unavailable.', 1;
                    """, ct);
            return tx;
        }
        catch { await tx.DisposeAsync(); throw; }
    }
    private static Result Fail(string code) => Result.Fail("La aplicación del pago requiere verificación o conciliación.", code);
}
