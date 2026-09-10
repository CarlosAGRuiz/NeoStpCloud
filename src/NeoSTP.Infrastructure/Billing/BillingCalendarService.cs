using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Billing;
using NeoSTP.Application.Common;
using NeoSTP.Domain.Core.Auditoria;
using NeoSTP.Domain.Core.Billing;
using NeoSTP.Infrastructure.Persistence;

namespace NeoSTP.Infrastructure.Billing;

public sealed class BillingCalendarService(NeoStpDbContext db, ICurrentUser user) : IBillingCalendarService
{
    public Task<Result> ApplyVerifiedPaymentAsync(int empresaId, int periodId, decimal amount, string currency,
        string reference, DateTime paidAtUtc, CancellationToken ct = default)
        => BillingCompanyTransaction.RunAsync(db, empresaId, async () =>
        {
            // Persisted central role, not a request-supplied actor or a tenant admin.
            var actor = user.IsAuthenticated && user.UserId is int uid
                ? await db.Usuarios.Include(x => x.Roles).ThenInclude(x => x.Rol).SingleOrDefaultAsync(x => x.Id == uid, ct) : null;
            if (actor is null || actor.EmpresaId != null || actor.EstadoCodigo != "ACTIVO"
                || !actor.Roles.Any(x => x.Rol.Codigo == "SUPERADMIN" && x.Rol.Activo && x.Rol.EsSistema))
                return Result.Fail("Solo administración central puede verificar pagos del acuerdo mensual.", "BILLING_FORBIDDEN");
            reference = reference?.Trim() ?? string.Empty;
            if (reference.Length is < 3 or > 200 || paidAtUtc.Kind != DateTimeKind.Utc || paidAtUtc > DateTime.UtcNow)
                return Result.Fail("Indique la referencia verificable y la fecha UTC del pago recibido.", "BILLING_PAYMENT_EVIDENCE_REQUIRED");
            var period = await db.BillingCalendarPeriods.Include(x => x.Invoice).Include(x => x.Agreement).ThenInclude(x => x.Subscription)
                .ThenInclude(x => x.Customer).SingleOrDefaultAsync(x => x.Id == periodId && x.Agreement.EmpresaId == empresaId, ct);
            if (period is null || period.Agreement.Subscription.Customer.EmpresaId != empresaId
                || period.Invoice.BillingSubscriptionId != period.Agreement.BillingSubscriptionId)
                return Result.Fail("Mensualidad no encontrada para esta empresa.", "BILLING_PERIOD_NOT_FOUND");
            if (amount != period.Invoice.Amount || currency != period.Invoice.Currency || amount <= 0)
                return Result.Fail("El pago debe coincidir con el importe y moneda de la mensualidad.", "BILLING_PAYMENT_AMOUNT_MISMATCH");
            if (period.BillingPaymentId != null)
                return period.PaymentReference == reference && period.Invoice.PaidAt == paidAtUtc
                    ? Result.Ok() : Result.Fail("La mensualidad ya tiene otro pago aplicado.", "BILLING_PAYMENT_CONFLICT");
            if (period.Invoice.Status != "OPEN" || await db.BillingCalendarPeriods.AnyAsync(x => x.AgreementId == period.AgreementId && x.PaymentReference == reference, ct))
                return Result.Fail("La referencia ya fue aplicada o la mensualidad no está abierta.", "BILLING_PAYMENT_CONFLICT");
            var payment = new BillingPayment { BillingSubscriptionId = period.Agreement.BillingSubscriptionId, Amount = amount,
                Currency = currency, Status = "SUCCEEDED", Metodo = "TRANSFERENCIA", PaidAt = paidAtUtc,
                VerificadoAt = DateTime.UtcNow, VerificadoPor = actor.Username, CreatedBy = actor.Id.ToString() };
            db.BillingPayments.Add(payment);
            await db.SaveChangesAsync(ct);
            period.BillingPaymentId = payment.Id;
            period.PaymentReference = reference;
            period.Invoice.Status = "PAID";
            period.Invoice.PaidAt = paidAtUtc;
            db.Auditoria.Add(new Auditoria { EmpresaId = empresaId, UsuarioId = actor.Id, Username = actor.Username,
                Modulo = "BILLING", Accion = "CALENDAR_PAYMENT_APPLIED", Entidad = "BillingCalendarPeriod", EntidadId = period.Id.ToString(),
                DatosDespues = JsonSerializer.Serialize(new { period.Id, PaymentId = payment.Id, amount, currency, paidAtUtc }),
                Detalle = "Pago real aplicado a mensualidad; período y licencia conservados." });
            await db.SaveChangesAsync(ct);
            return Result.Ok();
        }, ct);
}

/// <summary>Trusted scheduler/CLI operation. Never grants access or creates payments.</summary>
public static class BillingCalendarCycle
{
    public static Task<Result> AdvanceAsync(NeoStpDbContext db, int empresaId, DateTime nowUtc, CancellationToken ct = default)
        => BillingCompanyTransaction.RunAsync(db, empresaId, async () =>
        {
            var agreement = await db.BillingCalendarAgreements.Include(x => x.Subscription).ThenInclude(x => x.Customer)
                .SingleOrDefaultAsync(x => x.EmpresaId == empresaId && x.Active, ct);
            if (agreement is null) return Result.Ok();
            var sub = agreement.Subscription;
            if (sub.Customer.EmpresaId != empresaId || sub.PlanId != agreement.PlanId || agreement.AutomaticSuspension
                || agreement.TimeZoneId != "America/El_Salvador" || agreement.FirstPeriodStartLocal.Day != 1
                || agreement.MonthlyAmount <= 0 || string.IsNullOrWhiteSpace(agreement.Currency)
                || !string.IsNullOrWhiteSpace(sub.ExternalSubscriptionId)
                || await db.BillingPaymentApplications.AnyAsync(x => x.CheckoutIntent.EmpresaId == empresaId, ct))
                return Result.Fail("El acuerdo mensual requiere conciliación administrativa.", "BILLING_CALENDAR_DRIFT");
            // Explicit cancellation/suspension is authoritative and never reversed by the cycle.
            if (sub.Status != SubscriptionStatus.Active || sub.CancelAtPeriodEnd || sub.CanceledAt != null) return Result.Ok();
            var license = await db.EmpresaPlanes.SingleOrDefaultAsync(x => x.Id == agreement.EmpresaPlanId && x.EmpresaId == empresaId, ct);
            if (license is null || license.PlanId != agreement.PlanId || license.EstadoCodigo != "ACTIVO") return Result.Ok();
            var month = BillingCalendar.MonthAt(nowUtc);
            if (month < agreement.FirstPeriodStartLocal) return Result.Ok();
            var starts = await db.BillingCalendarPeriods.Where(x => x.AgreementId == agreement.Id).Select(x => x.PeriodStartLocal).ToListAsync(ct);
            var createdMonths = new List<DateOnly>();
            for (var date = agreement.FirstPeriodStartLocal; date <= month; date = date.AddMonths(1))
            {
                if (starts.Contains(date)) continue;
                db.BillingCalendarPeriods.Add(NewPeriod(agreement, date, nowUtc));
                createdMonths.Add(date);
            }
            if (createdMonths.Count > 0)
                db.Auditoria.Add(new Auditoria { EmpresaId = empresaId, Username = "calendar-billing", Modulo = "BILLING",
                    Accion = "CALENDAR_INVOICES_CREATED", Entidad = "BillingCalendarAgreement", EntidadId = agreement.Id.ToString(),
                    DatosDespues = JsonSerializer.Serialize(new { Months = createdMonths, agreement.MonthlyAmount, agreement.Currency, Status = "OPEN" }),
                    Detalle = "Mensualidades de mes calendario, cobro al último día local; acceso y pagos conservados." });
            sub.CurrentPeriodStart = BillingCalendar.StartUtc(month);
            sub.CurrentPeriodEnd = BillingCalendar.StartUtc(month.AddMonths(1));
            await db.SaveChangesAsync(ct);
            return Result.Ok();
        }, ct);

    public static BillingCalendarPeriod NewPeriod(BillingCalendarAgreement agreement, DateOnly month, DateTime nowUtc)
        => new() { Agreement = agreement, PeriodStartLocal = month, PeriodEndExclusiveLocal = month.AddMonths(1),
            DueLocalDate = BillingCalendar.DueDate(month), CreatedBy = "calendar-billing",
            Invoice = new BillingInvoice { BillingSubscriptionId = agreement.BillingSubscriptionId,
                Amount = agreement.MonthlyAmount, Currency = agreement.Currency, Status = "OPEN", InvoiceDate = nowUtc,
                DueDate = BillingCalendar.StartUtc(BillingCalendar.DueDate(month)), PaidAt = null, CreatedBy = "calendar-billing" } };
}
