using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Billing;
using NeoSTP.Application.Billing.Dtos;
using NeoSTP.Application.Common;
using NeoSTP.Application.Dte.Abstractions;
using NeoSTP.Domain.Core.Billing;
using NeoSTP.Domain.Core.Licenciamiento;
using NeoSTP.Domain.Common;
using NeoSTP.Domain.Core.Seguridad;
using NeoSTP.Infrastructure.Auth;
using NeoSTP.Infrastructure.Persistence;

namespace NeoSTP.Infrastructure.Billing;

public sealed partial class BillingService : IBillingService
{
    private readonly NeoStpDbContext _db;
    private readonly IPaymentProviderResolver _payments;
    private readonly IEmailSender _email;
    private readonly BillingOptions _options;
    private readonly ICurrentUser _currentUser;
    private readonly IBillingProviderOperationProcessor _providerOperations;

    public BillingService(
        NeoStpDbContext db,
        IPaymentProviderResolver payments,
        IEmailSender email,
        IOptions<BillingOptions> options,
        ICurrentUser currentUser,
        IBillingProviderOperationProcessor? providerOperations = null)
    {
        _db = db;
        _payments = payments;
        _email = email;
        _options = options.Value;
        _currentUser = currentUser;
        _providerOperations = providerOperations ?? new BillingProviderOperationProcessor(
            db, payments, email, options, NullLogger<BillingProviderOperationProcessor>.Instance);
    }

    /// <summary>Proveedor de pago para el método indicado (o el default).</summary>
    private IPaymentProvider P(string? metodo = null) => _payments.Resolve(metodo);

    // ─── Trial ────────────────────────────────────────────────────────────────

    public Task<Result<BillingSubscriptionDto>> StartTrialAsync(StartTrialRequest request, CancellationToken ct = default)
        => SingleAttemptAsync(() => StartTrialCoreAsync(request, ct));

    private async Task<Result<BillingSubscriptionDto>> StartTrialCoreAsync(StartTrialRequest request, CancellationToken ct)
    {
        if (!await CanAccessCompanyAsync(request.EmpresaId, manage: true, ct))
            return Result<BillingSubscriptionDto>.Fail("No tiene autorización para administrar la suscripción de esta empresa.", "BILLING_FORBIDDEN");
        if (_options.TrialDays <= 0)
            return Result<BillingSubscriptionDto>.Fail("La prueba gratuita no está disponible.", "BILLING_TRIAL_UNAVAILABLE");
        await using var transaction = await BeginBillingMutationAsync(request.EmpresaId, ct);
        if (await HasOpenCheckoutAsync(request.EmpresaId, ct))
            return Result<BillingSubscriptionDto>.Fail(OpenCheckoutMessage, OpenCheckoutCode);
        if (await HasOpenCancellationAsync(request.EmpresaId, ct))
            return Result<BillingSubscriptionDto>.Fail(OpenCancellationMessage, OpenCancellationCode);
        // A cancellation or expiration must not make an already-used trial available again.
        if (await _db.BillingSubscriptions.AnyAsync(s => s.Customer.EmpresaId == request.EmpresaId, ct))
            return Result<BillingSubscriptionDto>.Fail("La empresa ya tiene historial de suscripción y no puede iniciar otra prueba gratuita.", "BILLING_TRIAL_ALREADY_USED");

        var plan = await _db.Planes.FindAsync(new object[] { request.PlanId }, ct);
        if (plan is null || !plan.Activo)
            return Result<BillingSubscriptionDto>.Fail("Plan no encontrado.");

        var customer = await GetOrCreateCustomerAsync(request.EmpresaId, request.Email, metodo: null, ct);
        if (!customer.IsSuccess)
            return Result<BillingSubscriptionDto>.Fail(customer.Error!);

        var now = DateTime.UtcNow;
        var sub = new BillingSubscription
        {
            BillingCustomerId = customer.Value!.Id,
            PlanId = request.PlanId,
            Status = SubscriptionStatus.Trialing,
            TrialStart = now,
            TrialEnd = now.AddDays(_options.TrialDays),
        };

        _db.BillingSubscriptions.Add(sub);
        await ActivarLicenciaAsync(request.EmpresaId, request.PlanId, sub.TrialEnd, ct);
        await _db.SaveChangesAsync(ct);
        if (transaction is not null) await transaction.CommitAsync(ct);

        await _email.EnviarAsync(new()
        {
            To = request.Email,
            Subject = "Tu prueba gratuita ha comenzado",
            HtmlBody = $"<p>Tu período de prueba de <strong>{_options.TrialDays} días</strong> para el plan <strong>{plan.Nombre}</strong> ha iniciado y vence el <strong>{sub.TrialEnd:dd/MM/yyyy}</strong>.</p>",
        }, ct);

        return Result<BillingSubscriptionDto>.Ok(MapSubscription(sub, plan));
    }

    // ─── Checkout ─────────────────────────────────────────────────────────────

    // ─── Portal ───────────────────────────────────────────────────────────────

    public Task<Result<BillingPortalResult>> GetPortalUrlAsync(int empresaId, CancellationToken ct = default)
        => SingleAttemptAsync(() => GetPortalUrlCoreAsync(empresaId, ct));

    private async Task<Result<BillingPortalResult>> GetPortalUrlCoreAsync(int empresaId, CancellationToken ct)
    {
        if (!await CanAccessCompanyAsync(empresaId, manage: true, ct))
            return Result<BillingPortalResult>.Fail("No tiene autorización para administrar la suscripción de esta empresa.", "BILLING_FORBIDDEN");
        await using var transaction = await BeginBillingMutationAsync(empresaId, ct);
        if (await HasCalendarAgreementAsync(empresaId, ct))
            return Result<BillingPortalResult>.Fail(CalendarAgreementMessage, CalendarAgreementCode);
        if (await HasOpenCheckoutAsync(empresaId, ct))
            return Result<BillingPortalResult>.Fail(OpenCheckoutMessage, OpenCheckoutCode);
        if (await HasOpenCancellationAsync(empresaId, ct))
            return Result<BillingPortalResult>.Fail(OpenCancellationMessage, OpenCancellationCode);
        var customer = await _db.BillingCustomers.FirstOrDefaultAsync(c => c.EmpresaId == empresaId, ct);
        if (customer is null)
            return Result<BillingPortalResult>.Fail("Cliente de billing no encontrado.");

        var result = await P(customer.Provider).CreatePortalSessionAsync(
            customer.ExternalCustomerId ?? $"mock_cus_{empresaId}",
            "/billing",
            ct);
        if (transaction is not null) await transaction.CommitAsync(ct);
        return result;
    }

    // ─── Cambio de plan ───────────────────────────────────────────────────────

    public Task<Result> ChangePlanAsync(ChangePlanRequest request, CancellationToken ct = default)
        => SingleAttemptAsync(() => ChangePlanCoreAsync(request, ct));

    private async Task<Result> ChangePlanCoreAsync(ChangePlanRequest request, CancellationToken ct)
    {
        if (!await CanAccessCompanyAsync(request.EmpresaId, manage: true, ct))
            return Result.Fail("No tiene autorización para administrar la suscripción de esta empresa.", "BILLING_FORBIDDEN");
        await using var transaction = await BeginBillingMutationAsync(request.EmpresaId, ct);
        if (await HasCalendarAgreementAsync(request.EmpresaId, ct))
            return Result.Fail(CalendarAgreementMessage, CalendarAgreementCode);
        if (await HasOpenCheckoutAsync(request.EmpresaId, ct))
            return Result.Fail(OpenCheckoutMessage, OpenCheckoutCode);
        if (await HasOpenCancellationAsync(request.EmpresaId, ct))
            return Result.Fail(OpenCancellationMessage, OpenCancellationCode);
        var sub = await ActiveSubscriptionQuery(request.EmpresaId).FirstOrDefaultAsync(ct);
        if (sub is null)
            return Result.Fail("No se encontró suscripción activa.");

        // A plan change is not a payment or a new trial. Never discard the expiry
        // or grant a license from an incomplete, suspended, past-due or expired subscription.
        DateTime? licenseEnd = sub.Status switch
        {
            SubscriptionStatus.Trialing => sub.TrialEnd,
            SubscriptionStatus.Active => sub.CurrentPeriodEnd,
            _ => null,
        };
        if (licenseEnd is null || licenseEnd <= DateTime.UtcNow)
            return Result.Fail("La suscripción no tiene un período vigente. Confirme el pago o contacte a soporte antes de cambiar de plan.", "BILLING_SUBSCRIPTION_NOT_CURRENT");
        if (await HasPendingTransferAsync(request.EmpresaId, ct))
            return Result.Fail("Hay una transferencia pendiente de verificación. Resuélvala antes de cambiar de plan.", "BILLING_TRANSFER_PENDING");
        if (sub.Status == SubscriptionStatus.Active && string.IsNullOrWhiteSpace(sub.ExternalSubscriptionId))
            return Result.Fail("El cambio de un plan pagado por transferencia requiere verificar el nuevo pago antes de activarlo.", "BILLING_PAYMENT_REQUIRED");

        var plan = await _db.Planes.FindAsync(new object[] { request.NewPlanId }, ct);
        if (plan is null || !plan.Activo)
            return Result.Fail("Plan no encontrado.");

        if (sub.ExternalSubscriptionId != null)
        {
            var mapping = await _db.BillingPlanProviderMappings
                .Where(m => m.PlanId == request.NewPlanId && m.Provider == sub.Customer.Provider && m.IsActive)
                .FirstOrDefaultAsync(ct);
            var externalPlanId = mapping?.ExternalPlanId ?? $"mock_price_{request.NewPlanId}";
            var changeResult = await P(sub.Customer.Provider).ChangePlanAsync(sub.ExternalSubscriptionId, externalPlanId, ct);
            if (!changeResult.IsSuccess) return Result.Fail(changeResult.Error!);
        }

        sub.PlanId = request.NewPlanId;
        await ActivarLicenciaAsync(request.EmpresaId, request.NewPlanId, licenseEnd.Value, ct);
        await _db.SaveChangesAsync(ct);
        if (transaction is not null) await transaction.CommitAsync(ct);

        await _email.EnviarAsync(new()
        {
            To = (await _db.BillingCustomers.FindAsync(new object[] { sub.BillingCustomerId }, ct))?.Email ?? string.Empty,
            Subject = "Tu plan ha sido actualizado",
            HtmlBody = $"<p>Tu suscripción ha sido actualizada al plan <strong>{plan.Nombre}</strong>.</p>",
        }, ct);

        return Result.Ok();
    }

    // ─── Cancelación ──────────────────────────────────────────────────────────

    public async Task<Result> CancelSubscriptionAsync(CancelSubscriptionRequest request, CancellationToken ct = default)
    {
        var queued = await SingleAttemptAsync(() => QueueCancellationAsync(request, ct));
        if (queued.IsFailure)
            return Result.Fail(queued.Error!, queued.ErrorCode);
        return queued.Value is int operationId
            ? await _providerOperations.ProcessAsync(operationId, ct)
            : Result.Ok();
    }

    private async Task<Result<int?>> QueueCancellationAsync(CancelSubscriptionRequest request, CancellationToken ct)
    {
        if (!await CanAccessCompanyAsync(request.EmpresaId, manage: true, ct))
            return Result<int?>.Fail("No tiene autorización para administrar la suscripción de esta empresa.", "BILLING_FORBIDDEN");
        await using var transaction = await BeginBillingMutationAsync(request.EmpresaId, ct);
        if (await HasOpenCheckoutAsync(request.EmpresaId, ct))
            return Result<int?>.Fail(OpenCheckoutMessage, OpenCheckoutCode);
        if (await HasPendingTransferAsync(request.EmpresaId, ct))
            return Result<int?>.Fail("Hay una transferencia pendiente de verificación. Resuélvala antes de cancelar la suscripción.", "BILLING_TRANSFER_PENDING");
        var subscriptions = await ActiveSubscriptionQuery(request.EmpresaId).Take(2).ToListAsync(ct);
        if (subscriptions.Count > 1)
            return Result<int?>.Fail("Hay varias suscripciones vigentes. Soporte debe conciliarlas antes de cancelar.", "BILLING_SUBSCRIPTION_AMBIGUOUS");
        var sub = subscriptions.SingleOrDefault() ?? await _db.BillingSubscriptions.Include(s => s.Customer)
            .Where(s => s.Customer.EmpresaId == request.EmpresaId && s.Status == SubscriptionStatus.Canceled)
            .OrderByDescending(s => s.CreatedAt).ThenByDescending(s => s.Id).FirstOrDefaultAsync(ct);
        if (sub is null)
            return Result<int?>.Fail("No se encontró suscripción para cancelar.");

        var licenses = await _db.EmpresaPlanes.Where(ep => ep.EmpresaId == request.EmpresaId && ep.EstadoCodigo == "ACTIVO")
            .Take(2).ToListAsync(ct);
        // There is no subscription-to-license foreign key. Do not revoke a separate plan
        // or leave an older active row as a fallback when the association is ambiguous.
        if (licenses.Count > 1 || (licenses.Count == 1 && licenses[0].PlanId != sub.PlanId))
            return Result<int?>.Fail("La licencia no coincide de forma única con la suscripción. Soporte debe conciliarla antes de cancelar.", "BILLING_LICENSE_AMBIGUOUS");

        var now = DateTime.UtcNow;
        var alreadyCanceled = sub.Status == SubscriptionStatus.Canceled;
        var wasScheduled = alreadyCanceled && sub.CancelAtPeriodEnd;
        var license = licenses.SingleOrDefault();
        DateTime? periodEnd = sub.Status switch
        {
            SubscriptionStatus.Trialing => sub.TrialEnd,
            SubscriptionStatus.Active => sub.CurrentPeriodEnd,
            SubscriptionStatus.Canceled when wasScheduled => sub.CurrentPeriodEnd ?? sub.TrialEnd,
            _ => null,
        };
        // A requested scheduled cancellation cannot revive an immediately canceled
        // subscription, create a license, extend the original end, or invent a paid period.
        var preserveUntil = request.AtPeriodEnd && (!alreadyCanceled || wasScheduled) && license is not null
            ? periodEnd : null;
        if (preserveUntil is DateTime end && license?.FechaFin is DateTime licenseEnd && licenseEnd < end)
            preserveUntil = licenseEnd;
        var preserveAccess = preserveUntil is DateTime accessEnd && accessEnd > now;

        if (alreadyCanceled && (wasScheduled == request.AtPeriodEnd || !wasScheduled))
            return Result<int?>.Ok(null);

        var mode = request.AtPeriodEnd ? "PERIOD_END" : "IMMEDIATE";
        var idempotencyKey = $"neostp:billing:cancel:{sub.Id}:{mode}";
        var existingOperation = await _db.BillingProviderOperations
            .FirstOrDefaultAsync(o => o.IdempotencyKey == idempotencyKey, ct);
        if (existingOperation is not null)
        {
            if (existingOperation.EmpresaId != request.EmpresaId
                || existingOperation.BillingSubscriptionId != sub.Id
                || existingOperation.CancelAtPeriodEnd != request.AtPeriodEnd)
                return Result<int?>.Fail("La clave durable de cancelación no coincide con la empresa o suscripción.",
                    "BILLING_OPERATION_IDEMPOTENCY_CONFLICT");
            if (existingOperation.Status == BillingProviderOperationStatuses.Completed)
                return Result<int?>.Ok(null);
            if (existingOperation.Status == BillingProviderOperationStatuses.RequiresReconciliation)
                return Result<int?>.Fail("La cancelación requiere conciliación operativa; no se repetirá automáticamente.",
                    existingOperation.LastErrorCode ?? "BILLING_RECONCILIATION_REQUIRED");
            if (transaction is not null) await transaction.CommitAsync(ct);
            return Result<int?>.Ok(existingOperation.Id);
        }

        if (await HasOpenCancellationAsync(request.EmpresaId, ct))
            return Result<int?>.Fail(
                "Ya existe otra modalidad de cancelación pendiente o en conciliación. Debe resolverse antes de solicitar una diferente.",
                OpenCancellationCode);

        var operation = new BillingProviderOperation
        {
            EmpresaId = request.EmpresaId,
            BillingSubscriptionId = sub.Id,
            EmpresaPlanId = license?.Id,
            PlanId = sub.PlanId,
            Provider = sub.Customer.Provider,
            OperationType = BillingProviderOperationTypes.CancelSubscription,
            IdempotencyKey = idempotencyKey,
            ExternalResourceId = sub.ExternalSubscriptionId,
            CancelAtPeriodEnd = request.AtPeriodEnd,
            RequestedAt = now,
            AccessEndsAt = preserveAccess ? preserveUntil : null,
            Status = BillingProviderOperationStatuses.Pending,
            Attempts = 0,
            NextAttemptAt = now,
            CreatedAt = now,
            CreatedBy = _currentUser.UserId?.ToString(),
        };
        _db.BillingProviderOperations.Add(operation);
        await _db.SaveChangesAsync(ct);
        if (transaction is not null) await transaction.CommitAsync(ct);
        return Result<int?>.Ok(operation.Id);
    }

    // ─── Consultas ────────────────────────────────────────────────────────────

    public async Task<Result<BillingSubscriptionDto?>> GetActiveSubscriptionAsync(int empresaId, CancellationToken ct = default)
    {
        if (!await CanAccessCompanyAsync(empresaId, manage: false, ct))
            return Result<BillingSubscriptionDto?>.Fail("Empresa fuera de su alcance.", "BILLING_FORBIDDEN");
        var now = DateTime.UtcNow;
        var sub = await _db.BillingSubscriptions.Include(s => s.Customer)
            .Where(s => s.Customer.EmpresaId == empresaId
                && (s.Status != SubscriptionStatus.Canceled && s.Status != SubscriptionStatus.Expired
                    || s.Status == SubscriptionStatus.Canceled && s.CancelAtPeriodEnd
                    && _db.EmpresaPlanes.Any(ep => ep.EmpresaId == empresaId && ep.PlanId == s.PlanId
                        && ep.EstadoCodigo == "ACTIVO" && ep.FechaInicio <= now
                        && ep.FechaFin != null && ep.FechaFin > now)))
            .Include(s => s.Plan)
            .OrderByDescending(s => s.CreatedAt).ThenByDescending(s => s.Id)
            .FirstOrDefaultAsync(ct);

        if (sub is null)
            return Result<BillingSubscriptionDto?>.Ok(null);

        var dto = MapSubscription(sub, sub.Plan);
        var agreement = await _db.BillingCalendarAgreements.AsNoTracking()
            .SingleOrDefaultAsync(x => x.EmpresaId == empresaId && x.BillingSubscriptionId == sub.Id && x.Active, ct);
        if (agreement != null)
        {
            var month = BillingCalendar.MonthAt(DateTime.UtcNow);
            var due = await _db.BillingCalendarPeriods.Where(x => x.AgreementId == agreement.Id && x.Invoice.Status == "OPEN")
                .OrderBy(x => x.DueLocalDate).Select(x => (DateOnly?)x.DueLocalDate).FirstOrDefaultAsync(ct);
            var currentPaid = await _db.BillingCalendarPeriods.AnyAsync(x => x.AgreementId == agreement.Id
                && x.PeriodStartLocal == month && x.Invoice.Status == "PAID", ct);
            dto = dto with { CalendarBilling = true, NextDueLocalDate = due ?? BillingCalendar.DueDate(currentPaid ? month.AddMonths(1) : month),
                ServicePeriodStartLocal = month, ServicePeriodEndExclusiveLocal = month.AddMonths(1) };
        }
        return Result<BillingSubscriptionDto?>.Ok(dto);
    }

    public async Task<Result<IReadOnlyList<BillingPaymentDto>>> GetPaymentsAsync(int empresaId, CancellationToken ct = default)
    {
        if (!await CanAccessCompanyAsync(empresaId, manage: false, ct))
            return Result<IReadOnlyList<BillingPaymentDto>>.Fail("Empresa fuera de su alcance.", "BILLING_FORBIDDEN");
        var list = await _db.BillingPayments
            .Include(p => p.Subscription).ThenInclude(s => s.Customer)
            .Where(p => p.Subscription.Customer.EmpresaId == empresaId)
            .OrderByDescending(p => p.PaidAt)
            .Select(p => new BillingPaymentDto(p.Id, p.Amount, p.Currency, p.Status, p.PaidAt, p.ReceiptUrl))
            .ToListAsync(ct);

        return Result<IReadOnlyList<BillingPaymentDto>>.Ok(list);
    }

    public async Task<Result<IReadOnlyList<BillingInvoiceDto>>> GetInvoicesAsync(int empresaId, CancellationToken ct = default)
    {
        if (!await CanAccessCompanyAsync(empresaId, manage: false, ct))
            return Result<IReadOnlyList<BillingInvoiceDto>>.Fail("Empresa fuera de su alcance.", "BILLING_FORBIDDEN");
        var list = await _db.BillingInvoices
            .Include(i => i.Subscription).ThenInclude(s => s.Customer)
            .Where(i => i.Subscription.Customer.EmpresaId == empresaId)
            .OrderByDescending(i => i.InvoiceDate)
            .Select(i => new BillingInvoiceDto(i.Id, i.Amount, i.Currency, i.Status, i.InvoiceDate, i.DueDate, i.PdfUrl,
                _db.BillingCalendarPeriods.Where(p => p.BillingInvoiceId == i.Id && p.Agreement.EmpresaId == empresaId)
                    .Select(p => (DateOnly?)p.DueLocalDate).FirstOrDefault()))
            .ToListAsync(ct);

        return Result<IReadOnlyList<BillingInvoiceDto>>.Ok(list);
    }

    // ─── Transferencia bancaria (verificación manual) ──────────────────────────

    private const string TransferMetodo = "TRANSFERENCIA";
    private const string PendienteVerif = "PENDIENTE_VERIFICACION";

    public Task<Result<TransferenciaInstruccionesDto>> IniciarTransferenciaAsync(IniciarTransferenciaRequest request, CancellationToken ct = default)
        => SingleAttemptAsync(() => IniciarTransferenciaCoreAsync(request, ct));

    private async Task<Result<TransferenciaInstruccionesDto>> IniciarTransferenciaCoreAsync(IniciarTransferenciaRequest request, CancellationToken ct)
    {
        if (!await CanAccessCompanyAsync(request.EmpresaId, manage: true, ct))
            return Result<TransferenciaInstruccionesDto>.Fail("No tiene autorización para administrar la suscripción de esta empresa.", "BILLING_FORBIDDEN");
        await using var transaction = await BeginBillingMutationAsync(request.EmpresaId, ct);
        if (await HasCalendarAgreementAsync(request.EmpresaId, ct))
            return Result<TransferenciaInstruccionesDto>.Fail(CalendarAgreementMessage, CalendarAgreementCode);
        if (await _db.BillingPaymentApplications.AnyAsync(x => x.CheckoutIntent.EmpresaId == request.EmpresaId, ct))
            return Result<TransferenciaInstruccionesDto>.Fail("Las condiciones pagadas se administran mediante Billing.", "LICENSE_MANAGED_BY_BILLING");
        if (await HasOpenCheckoutAsync(request.EmpresaId, ct))
            return Result<TransferenciaInstruccionesDto>.Fail(OpenCheckoutMessage, OpenCheckoutCode);
        if (await HasOpenCancellationAsync(request.EmpresaId, ct))
            return Result<TransferenciaInstruccionesDto>.Fail(OpenCancellationMessage, OpenCancellationCode);
        if (await HasPendingTransferAsync(request.EmpresaId, ct))
            return Result<TransferenciaInstruccionesDto>.Fail("Ya existe una transferencia pendiente de verificación. No se puede sustituir su plan ni registrar otra hasta resolverla.", "BILLING_TRANSFER_PENDING");
        var plan = await _db.Planes.FindAsync(new object[] { request.PlanId }, ct);
        if (plan is null || !plan.Activo)
            return Result<TransferenciaInstruccionesDto>.Fail("Plan no encontrado.", "PLAN_NOT_FOUND");

        var customer = await GetOrCreateCustomerAsync(request.EmpresaId, string.Empty, "Transferencia", ct);
        if (!customer.IsSuccess) return Result<TransferenciaInstruccionesDto>.Fail(customer.Error!);

        var sub = await ActiveSubscriptionQuery(request.EmpresaId).FirstOrDefaultAsync(ct);
        if (sub is null)
        {
            sub = new BillingSubscription
            {
                BillingCustomerId = customer.Value!.Id,
                PlanId = request.PlanId,
                Status = SubscriptionStatus.Incomplete,
            };
            _db.BillingSubscriptions.Add(sub);
        }
        else
        {
            sub.PlanId = request.PlanId;
        }
        await _db.SaveChangesAsync(ct);

        var pago = new BillingPayment
        {
            BillingSubscriptionId = sub.Id,
            Amount = plan.PrecioMensual,
            Currency = plan.MonedaCodigo,
            Status = PendienteVerif,
            Metodo = TransferMetodo,
        };
        _db.BillingPayments.Add(pago);
        await _db.SaveChangesAsync(ct);
        if (transaction is not null) await transaction.CommitAsync(ct);

        var t = _options.Transferencia;
        return Result<TransferenciaInstruccionesDto>.Ok(new TransferenciaInstruccionesDto(
            pago.Id, plan.PrecioMensual, plan.MonedaCodigo, t.Banco, t.TipoCuenta, t.NumeroCuenta, t.Titular, t.Instrucciones));
    }

    public async Task<Result> RegistrarComprobanteAsync(int empresaId, int paymentId, string comprobanteUrl, CancellationToken ct = default)
    {
        if (!await CanAccessCompanyAsync(empresaId, manage: true, ct))
            return Result.Fail("No tiene autorización para administrar la suscripción de esta empresa.", "BILLING_FORBIDDEN");
        if (await HasOpenCancellationAsync(empresaId, ct))
            return Result.Fail(OpenCancellationMessage, OpenCancellationCode);
        var pago = await _db.BillingPayments
            .Include(p => p.Subscription).ThenInclude(s => s.Customer)
            .FirstOrDefaultAsync(p => p.Id == paymentId && p.Subscription.Customer.EmpresaId == empresaId, ct);
        if (pago is null) return Result.Fail("Pago no encontrado.", "PAGO_NOT_FOUND");

        pago.ComprobanteUrl = comprobanteUrl;
        await _db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public Task<Result> ConfirmarTransferenciaAsync(int paymentId, string actor, CancellationToken ct = default)
        => SingleAttemptAsync(() => ConfirmarTransferenciaCoreAsync(paymentId, ct));

    private async Task<Result> ConfirmarTransferenciaCoreAsync(int paymentId, CancellationToken ct)
    {
        var administrator = await PlatformAdministratorAsync(ct);
        if (administrator is null)
            return Result.Fail("Solo la administración central puede verificar transferencias de suscripción.", "BILLING_FORBIDDEN");
        var empresaId = await PaymentCompanyAsync(paymentId, ct);
        if (empresaId is null) return Result.Fail("Pago no encontrado.", "PAGO_NOT_FOUND");
        await using var transaction = await BeginBillingMutationAsync(empresaId.Value, ct);
        if (await _db.BillingPaymentApplications.AnyAsync(x => x.CheckoutIntent.EmpresaId == empresaId.Value, ct))
            return Result.Fail("Las condiciones pagadas se administran mediante Billing.", "LICENSE_MANAGED_BY_BILLING");
        if (await HasOpenCheckoutAsync(empresaId.Value, ct))
            return Result.Fail(OpenCheckoutMessage, OpenCheckoutCode);
        if (await HasOpenCancellationAsync(empresaId.Value, ct))
            return Result.Fail(OpenCancellationMessage, OpenCancellationCode);
        var pago = await _db.BillingPayments
            .Include(p => p.Subscription).ThenInclude(s => s.Customer)
            .FirstOrDefaultAsync(p => p.Id == paymentId, ct);
        if (pago is null) return Result.Fail("Pago no encontrado.", "PAGO_NOT_FOUND");
        if (pago.Metodo != TransferMetodo || pago.Status != PendienteVerif) return Result.Fail("El pago no es una transferencia pendiente de verificación.", "ESTADO_INVALIDO");

        var sub = pago.Subscription;
        if (sub.Status is SubscriptionStatus.Canceled or SubscriptionStatus.Expired)
            return Result.Fail("La transferencia pertenece a una suscripción cancelada o vencida; requiere revisión antes de activarla.", "BILLING_SUBSCRIPTION_NOT_CURRENT");
        var plan = await _db.Planes.FindAsync(new object[] { sub.PlanId }, ct);
        if (plan is null || !plan.Activo || pago.Amount != plan.PrecioMensual
            || !string.Equals(pago.Currency, plan.MonedaCodigo, StringComparison.OrdinalIgnoreCase))
            return Result.Fail("El importe o la moneda de la transferencia no corresponde al plan actual. Rechace y registre el pago correcto; no se ha activado la licencia.", "BILLING_TRANSFER_PLAN_MISMATCH");
        if (await PendingTransfersQuery(empresaId.Value).CountAsync(ct) != 1)
            return Result.Fail("La empresa tiene más de una transferencia pendiente; rechace las solicitudes duplicadas antes de confirmar.", "BILLING_TRANSFER_AMBIGUOUS");

        var now = DateTime.UtcNow;
        pago.Status = "SUCCEEDED";
        pago.VerificadoPor = administrator.Username;
        pago.VerificadoAt = now;
        pago.PaidAt = now;

        sub.Status = SubscriptionStatus.Active;
        sub.CurrentPeriodEnd = now.AddMonths(1);
        await ActivarLicenciaAsync(sub.Customer.EmpresaId, sub.PlanId, sub.CurrentPeriodEnd.Value, ct);
        await _db.SaveChangesAsync(ct);
        if (transaction is not null) await transaction.CommitAsync(ct);

        if (!string.IsNullOrWhiteSpace(sub.Customer.Email))
        {
            await _email.EnviarAsync(new()
            {
                To = sub.Customer.Email,
                Subject = "Pago por transferencia confirmado",
                HtmlBody = $"<p>Tu pago por transferencia de <strong>{pago.Currency} {pago.Amount:N2}</strong> fue verificado. Tu suscripción está activa hasta el <strong>{sub.CurrentPeriodEnd:dd/MM/yyyy}</strong>.</p>",
            }, ct);
        }
        return Result.Ok();
    }

    public Task<Result> RechazarTransferenciaAsync(int paymentId, string motivo, string actor, CancellationToken ct = default)
        => SingleAttemptAsync(() => RechazarTransferenciaCoreAsync(paymentId, motivo, ct));

    private async Task<Result> RechazarTransferenciaCoreAsync(int paymentId, string motivo, CancellationToken ct)
    {
        var administrator = await PlatformAdministratorAsync(ct);
        if (administrator is null)
            return Result.Fail("Solo la administración central puede verificar transferencias de suscripción.", "BILLING_FORBIDDEN");
        var empresaId = await PaymentCompanyAsync(paymentId, ct);
        if (empresaId is null) return Result.Fail("Pago no encontrado.", "PAGO_NOT_FOUND");
        await using var transaction = await BeginBillingMutationAsync(empresaId.Value, ct);
        if (await HasCalendarAgreementAsync(empresaId.Value, ct))
            return Result.Fail(CalendarAgreementMessage, CalendarAgreementCode);
        if (await HasOpenCheckoutAsync(empresaId.Value, ct))
            return Result.Fail(OpenCheckoutMessage, OpenCheckoutCode);
        if (await HasOpenCancellationAsync(empresaId.Value, ct))
            return Result.Fail(OpenCancellationMessage, OpenCancellationCode);
        var pago = await _db.BillingPayments
            .Include(p => p.Subscription).ThenInclude(s => s.Customer)
            .FirstOrDefaultAsync(p => p.Id == paymentId, ct);
        if (pago is null) return Result.Fail("Pago no encontrado.", "PAGO_NOT_FOUND");
        if (pago.Metodo != TransferMetodo || pago.Status != PendienteVerif) return Result.Fail("El pago no es una transferencia pendiente de verificación.", "ESTADO_INVALIDO");

        pago.Status = "FAILED";
        pago.FailureReason = motivo;
        pago.VerificadoPor = administrator.Username;
        pago.VerificadoAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        if (transaction is not null) await transaction.CommitAsync(ct);

        if (!string.IsNullOrWhiteSpace(pago.Subscription.Customer.Email))
        {
            await _email.EnviarAsync(new()
            {
                To = pago.Subscription.Customer.Email,
                Subject = "Pago por transferencia rechazado",
                HtmlBody = $"<p>Tu comprobante de transferencia fue rechazado. Motivo: <strong>{motivo}</strong>. Por favor verifica e inténtalo de nuevo.</p>",
            }, ct);
        }
        return Result.Ok();
    }

    public async Task<Result<IReadOnlyList<TransferenciaPendienteDto>>> GetTransferenciasPendientesAsync(int? empresaId, CancellationToken ct = default)
    {
        if (await PlatformAdministratorAsync(ct) is null)
            return Result<IReadOnlyList<TransferenciaPendienteDto>>.Fail("Solo la administración central puede revisar transferencias de suscripción.", "BILLING_FORBIDDEN");
        var query =
            from p in _db.BillingPayments
            join e in _db.Empresas on p.Subscription.Customer.EmpresaId equals e.Id
            where p.Metodo == TransferMetodo && p.Status == PendienteVerif
            select new { p, e, planNombre = p.Subscription.Plan.Nombre };

        if (empresaId is int eid)
            query = query.Where(x => x.e.Id == eid);

        var list = await query
            .OrderByDescending(x => x.p.CreatedAt)
            .Select(x => new TransferenciaPendienteDto(
                x.p.Id, x.e.Id, x.e.RazonSocial, x.planNombre, x.p.Amount, x.p.Currency, x.p.ComprobanteUrl, x.p.CreatedAt))
            .ToListAsync(ct);

        return Result<IReadOnlyList<TransferenciaPendienteDto>>.Ok(list);
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    // The configured SQL retry strategy must never replay a provider call or an email.
    // A zero-retry strategy allows explicit transactions without repeating this workflow.
    private Task<T> SingleAttemptAsync<T>(Func<Task<T>> operation)
        => new BillingSingleAttemptStrategy(_db).ExecuteAsync(operation);

    private sealed class BillingSingleAttemptStrategy(DbContext context) : ExecutionStrategy(context, 0, TimeSpan.Zero)
    {
        protected override bool ShouldRetryOn(Exception exception) => false;
    }

    private async Task<IDbContextTransaction?> BeginBillingMutationAsync(int empresaId, CancellationToken ct)
    {
        if (!_db.Database.IsRelational()) return null;
        var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            if (_db.Database.IsSqlServer())
            {
                var resource = $"NeoSTP:BILLING:{empresaId}";
                await _db.Database.ExecuteSqlInterpolatedAsync($"""
                    DECLARE @result int;
                    EXEC @result = sys.sp_getapplock @Resource = {resource},
                        @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 15000;
                    IF @result < 0 THROW 50001, 'No fue posible reservar la modificación de la suscripción.', 1;
                    """, ct);
            }
            return transaction;
        }
        catch
        {
            await transaction.DisposeAsync();
            throw;
        }
    }

    private IQueryable<BillingPayment> PendingTransfersQuery(int empresaId)
        => _db.BillingPayments.Where(p => p.Subscription.Customer.EmpresaId == empresaId
            && p.Metodo == TransferMetodo && p.Status == PendienteVerif);

    private Task<bool> HasPendingTransferAsync(int empresaId, CancellationToken ct)
        => PendingTransfersQuery(empresaId).AnyAsync(ct);

    private const string OpenCancellationCode = "BILLING_CANCELLATION_PENDING";
    private const string OpenCancellationMessage =
        "Hay una cancelación pendiente o en conciliación. Soporte debe resolverla antes de realizar otra modificación de facturación.";

    private Task<bool> HasOpenCancellationAsync(int empresaId, CancellationToken ct)
        => _db.BillingProviderOperations.AsNoTracking().AnyAsync(o => o.EmpresaId == empresaId
            && o.OperationType == BillingProviderOperationTypes.CancelSubscription
            && o.Status != BillingProviderOperationStatuses.Completed, ct);

    private Task<int?> PaymentCompanyAsync(int paymentId, CancellationToken ct)
        => _db.BillingPayments.AsNoTracking().Where(p => p.Id == paymentId)
            .Select(p => (int?)p.Subscription.Customer.EmpresaId).SingleOrDefaultAsync(ct);

    private async Task<Usuario?> AuthenticatedUserAsync(CancellationToken ct)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is not int userId) return null;
        return await _db.Usuarios.AsNoTracking().Include(u => u.Roles).ThenInclude(r => r.Rol)
            .FirstOrDefaultAsync(u => u.Id == userId && u.EstadoCodigo == EstadoCodes.Activo
                && (u.BloqueadoHasta == null || u.BloqueadoHasta <= DateTime.UtcNow), ct);
    }

    private async Task<Usuario?> PlatformAdministratorAsync(CancellationToken ct)
    {
        if (!RbacSecurity.IsPlatformAdministrator(_currentUser)) return null;
        var user = await AuthenticatedUserAsync(ct);
        return user is not null && RbacSecurity.IsPlatformUser(user) ? user : null;
    }

    private async Task<bool> CanAccessCompanyAsync(int empresaId, bool manage, CancellationToken ct)
    {
        var user = await AuthenticatedUserAsync(ct);
        if (user is null) return false;
        if (RbacSecurity.IsPlatformAdministrator(_currentUser) && RbacSecurity.IsPlatformUser(user)) return true;
        if (_currentUser.EmpresaId != empresaId || user.TipoUsuarioCodigo == "SUPERADMIN") return false;
        if (user.EmpresaId == empresaId)
            return !manage || (user.TipoUsuarioCodigo == "ADMIN" && _currentUser.TipoUsuarioCodigo == "ADMIN");

        // An additional-company membership is read-only for billing unless its persisted
        // active role is ADMIN. A tenant role named SUPERADMIN never grants central access.
        return await _db.UsuarioEmpresas.AsNoTracking().AnyAsync(m => m.UsuarioId == user.Id
            && m.EmpresaId == empresaId && m.EstadoCodigo == EstadoCodes.Activo && m.Rol.Activo
            && (m.Rol.EmpresaId == null || m.Rol.EmpresaId == empresaId)
            && m.Rol.Codigo != "SUPERADMIN"
            && (!manage || m.Rol.Codigo == "ADMIN"), ct);
    }

    private IQueryable<BillingSubscription> ActiveSubscriptionQuery(int empresaId)
        => _db.BillingSubscriptions
              .Include(s => s.Customer)
              .Where(s => s.Customer.EmpresaId == empresaId
                       && s.Status != SubscriptionStatus.Canceled
                       && s.Status != SubscriptionStatus.Expired);

    private async Task<Result<BillingCustomer>> GetOrCreateCustomerAsync(int empresaId, string email, string? metodo, CancellationToken ct)
    {
        var customer = await _db.BillingCustomers.FirstOrDefaultAsync(c => c.EmpresaId == empresaId, ct);
        if (customer != null)
            return Result<BillingCustomer>.Ok(customer);

        var provider = P(metodo);
        var extResult = await provider.CreateCustomerAsync(email, empresaId, ct);
        if (!extResult.IsSuccess)
            return Result<BillingCustomer>.Fail(extResult.Error!);

        customer = new BillingCustomer
        {
            EmpresaId = empresaId,
            Email = email,
            Provider = provider.ProviderName,
            ExternalCustomerId = extResult.Value,
        };

        _db.BillingCustomers.Add(customer);
        await _db.SaveChangesAsync(ct);
        return Result<BillingCustomer>.Ok(customer);
    }

    private async Task ActivarLicenciaAsync(int empresaId, int planId, DateTime fin, CancellationToken ct)
    {
        var ahora = DateTime.UtcNow;
        var empresaPlan = await _db.EmpresaPlanes
            .Where(ep => ep.EmpresaId == empresaId)
            .OrderByDescending(ep => ep.FechaInicio)
            .FirstOrDefaultAsync(ct);

        if (empresaPlan != null)
        {
            empresaPlan.PlanId = planId;
            empresaPlan.FechaFin = fin;
            empresaPlan.EstadoCodigo = "ACTIVO";
        }
        else
        {
            _db.EmpresaPlanes.Add(new EmpresaPlan
            {
                EmpresaId = empresaId,
                PlanId = planId,
                FechaInicio = ahora,
                FechaFin = fin,
                EstadoCodigo = "ACTIVO",
            });
        }

        // La licencia comercial y los módulos operativos deben activarse juntos.
        // Sin esta sincronización, una suscripción válida queda sin acceso a CORE/NeoDTE.
        var modulosPlan = await _db.PlanModulos
            .Where(pm => pm.PlanId == planId && pm.Activo)
            .Select(pm => pm.ModuloId)
            .ToListAsync(ct);

        var modulosEmpresa = await _db.EmpresaModulos
            .Where(em => em.EmpresaId == empresaId)
            .ToDictionaryAsync(em => em.ModuloId, ct);

        foreach (var moduloId in modulosPlan)
        {
            if (modulosEmpresa.TryGetValue(moduloId, out var moduloEmpresa))
            {
                if (!moduloEmpresa.Activo)
                {
                    moduloEmpresa.Activo = true;
                    moduloEmpresa.FechaActivacion = ahora;
                    moduloEmpresa.FechaInactivacion = null;
                }

                continue;
            }

            _db.EmpresaModulos.Add(new EmpresaModulo
            {
                EmpresaId = empresaId,
                ModuloId = moduloId,
                Activo = true,
                FechaActivacion = ahora,
            });
        }
    }

    private static BillingSubscriptionDto MapSubscription(BillingSubscription s, Plan plan)
        => new(s.Id, s.BillingCustomerId, s.PlanId, plan.Nombre, s.Status, s.TrialEnd, s.CurrentPeriodEnd, s.CancelAtPeriodEnd);

    private const string CalendarAgreementCode = "BILLING_CALENDAR_MANAGED";
    private const string CalendarAgreementMessage = "La empresa tiene un acuerdo mensual con cobro a fin de mes. Administración debe aplicar el pago a su mensualidad o conciliar el acuerdo antes de cambiar de plan o proveedor.";
    private Task<bool> HasCalendarAgreementAsync(int empresaId, CancellationToken ct)
        => _db.BillingCalendarAgreements.AnyAsync(x => x.EmpresaId == empresaId && x.Active, ct);
}
