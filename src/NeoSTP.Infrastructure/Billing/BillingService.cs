using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NeoSTP.Application.Billing;
using NeoSTP.Application.Billing.Dtos;
using NeoSTP.Application.Common;
using NeoSTP.Application.Dte.Abstractions;
using NeoSTP.Domain.Core.Billing;
using NeoSTP.Domain.Core.Licenciamiento;
using NeoSTP.Infrastructure.Persistence;

namespace NeoSTP.Infrastructure.Billing;

public sealed class BillingService : IBillingService
{
    private readonly NeoStpDbContext _db;
    private readonly IPaymentProviderResolver _payments;
    private readonly IEmailSender _email;
    private readonly BillingOptions _options;

    public BillingService(
        NeoStpDbContext db,
        IPaymentProviderResolver payments,
        IEmailSender email,
        IOptions<BillingOptions> options)
    {
        _db = db;
        _payments = payments;
        _email = email;
        _options = options.Value;
    }

    /// <summary>Proveedor de pago para el método indicado (o el default).</summary>
    private IPaymentProvider P(string? metodo = null) => _payments.Resolve(metodo);

    // ─── Trial ────────────────────────────────────────────────────────────────

    public async Task<Result<BillingSubscriptionDto>> StartTrialAsync(StartTrialRequest request, CancellationToken ct = default)
    {
        var existing = await ActiveSubscriptionQuery(request.EmpresaId).FirstOrDefaultAsync(ct);
        if (existing != null)
            return Result<BillingSubscriptionDto>.Fail("La empresa ya tiene una suscripción activa o en trial.");

        var plan = await _db.Planes.FindAsync(new object[] { request.PlanId }, ct);
        if (plan is null)
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

        await _email.EnviarAsync(new()
        {
            To = request.Email,
            Subject = "Tu prueba gratuita ha comenzado",
            HtmlBody = $"<p>Tu período de prueba de <strong>{_options.TrialDays} días</strong> para el plan <strong>{plan.Nombre}</strong> ha iniciado y vence el <strong>{sub.TrialEnd:dd/MM/yyyy}</strong>.</p>",
        }, ct);

        return Result<BillingSubscriptionDto>.Ok(MapSubscription(sub, plan));
    }

    // ─── Checkout ─────────────────────────────────────────────────────────────

    public async Task<Result<CheckoutSessionResult>> CreateCheckoutSessionAsync(CreateCheckoutRequest request, CancellationToken ct = default)
    {
        var provider = P(request.Metodo);
        var customer = await GetOrCreateCustomerAsync(request.EmpresaId, string.Empty, request.Metodo, ct);
        if (!customer.IsSuccess) return Result<CheckoutSessionResult>.Fail(customer.Error!);

        var mapping = await _db.BillingPlanProviderMappings
            .Where(m => m.PlanId == request.PlanId && m.Provider == provider.ProviderName && m.IsActive)
            .FirstOrDefaultAsync(ct);

        var externalPlanId = mapping?.ExternalPlanId ?? $"mock_price_{request.PlanId}";

        return await provider.CreateCheckoutSessionAsync(
            customer.Value!.ExternalCustomerId ?? $"mock_cus_{request.EmpresaId}",
            externalPlanId,
            request.ReturnUrl,
            _options.Stripe.CancelUrl,
            ct);
    }

    // ─── Portal ───────────────────────────────────────────────────────────────

    public async Task<Result<BillingPortalResult>> GetPortalUrlAsync(int empresaId, CancellationToken ct = default)
    {
        var customer = await _db.BillingCustomers.FirstOrDefaultAsync(c => c.EmpresaId == empresaId, ct);
        if (customer is null)
            return Result<BillingPortalResult>.Fail("Cliente de billing no encontrado.");

        return await P(customer.Provider).CreatePortalSessionAsync(
            customer.ExternalCustomerId ?? $"mock_cus_{empresaId}",
            "/billing",
            ct);
    }

    // ─── Cambio de plan ───────────────────────────────────────────────────────

    public async Task<Result> ChangePlanAsync(ChangePlanRequest request, CancellationToken ct = default)
    {
        var sub = await ActiveSubscriptionQuery(request.EmpresaId).FirstOrDefaultAsync(ct);
        if (sub is null)
            return Result.Fail("No se encontró suscripción activa.");

        var plan = await _db.Planes.FindAsync(new object[] { request.NewPlanId }, ct);
        if (plan is null)
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
        await ActivarLicenciaAsync(request.EmpresaId, request.NewPlanId, sub.CurrentPeriodEnd, ct);
        await _db.SaveChangesAsync(ct);

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
        var sub = await ActiveSubscriptionQuery(request.EmpresaId).FirstOrDefaultAsync(ct);
        if (sub is null)
            return Result.Fail("No se encontró suscripción activa.");

        if (sub.ExternalSubscriptionId != null)
        {
            var cancelResult = await P(sub.Customer.Provider).CancelSubscriptionAsync(sub.ExternalSubscriptionId, request.AtPeriodEnd, ct);
            if (!cancelResult.IsSuccess) return Result.Fail(cancelResult.Error!);
        }

        sub.Status = SubscriptionStatus.Canceled;
        sub.CanceledAt = DateTime.UtcNow;
        sub.CancelAtPeriodEnd = request.AtPeriodEnd;
        await _db.SaveChangesAsync(ct);

        await _email.EnviarAsync(new()
        {
            To = (await _db.BillingCustomers.FindAsync(new object[] { sub.BillingCustomerId }, ct))?.Email ?? string.Empty,
            Subject = "Tu suscripción ha sido cancelada",
            HtmlBody = request.AtPeriodEnd
                ? $"<p>Tu suscripción ha sido cancelada. Mantendrás acceso hasta el <strong>{sub.CurrentPeriodEnd:dd/MM/yyyy}</strong>.</p>"
                : "<p>Tu suscripción ha sido cancelada inmediatamente.</p>",
        }, ct);

        return Result.Ok();
    }

    // ─── Consultas ────────────────────────────────────────────────────────────

    public async Task<Result<BillingSubscriptionDto?>> GetActiveSubscriptionAsync(int empresaId, CancellationToken ct = default)
    {
        var sub = await ActiveSubscriptionQuery(empresaId)
            .Include(s => s.Plan)
            .FirstOrDefaultAsync(ct);

        if (sub is null)
            return Result<BillingSubscriptionDto?>.Ok(null);

        return Result<BillingSubscriptionDto?>.Ok(MapSubscription(sub, sub.Plan));
    }

    public async Task<Result<IReadOnlyList<BillingPaymentDto>>> GetPaymentsAsync(int empresaId, CancellationToken ct = default)
    {
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
        var list = await _db.BillingInvoices
            .Include(i => i.Subscription).ThenInclude(s => s.Customer)
            .Where(i => i.Subscription.Customer.EmpresaId == empresaId)
            .OrderByDescending(i => i.InvoiceDate)
            .Select(i => new BillingInvoiceDto(i.Id, i.Amount, i.Currency, i.Status, i.InvoiceDate, i.DueDate, i.PdfUrl))
            .ToListAsync(ct);

        return Result<IReadOnlyList<BillingInvoiceDto>>.Ok(list);
    }

    // ─── Transferencia bancaria (verificación manual) ──────────────────────────

    private const string TransferMetodo = "TRANSFERENCIA";
    private const string PendienteVerif = "PENDIENTE_VERIFICACION";

    public async Task<Result<TransferenciaInstruccionesDto>> IniciarTransferenciaAsync(IniciarTransferenciaRequest request, CancellationToken ct = default)
    {
        var plan = await _db.Planes.FindAsync(new object[] { request.PlanId }, ct);
        if (plan is null)
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

        var t = _options.Transferencia;
        return Result<TransferenciaInstruccionesDto>.Ok(new TransferenciaInstruccionesDto(
            pago.Id, plan.PrecioMensual, plan.MonedaCodigo, t.Banco, t.TipoCuenta, t.NumeroCuenta, t.Titular, t.Instrucciones));
    }

    public async Task<Result> RegistrarComprobanteAsync(int empresaId, int paymentId, string comprobanteUrl, CancellationToken ct = default)
    {
        var pago = await _db.BillingPayments
            .Include(p => p.Subscription).ThenInclude(s => s.Customer)
            .FirstOrDefaultAsync(p => p.Id == paymentId && p.Subscription.Customer.EmpresaId == empresaId, ct);
        if (pago is null) return Result.Fail("Pago no encontrado.", "PAGO_NOT_FOUND");

        pago.ComprobanteUrl = comprobanteUrl;
        await _db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<Result> ConfirmarTransferenciaAsync(int paymentId, string actor, CancellationToken ct = default)
    {
        var pago = await _db.BillingPayments
            .Include(p => p.Subscription).ThenInclude(s => s.Customer)
            .FirstOrDefaultAsync(p => p.Id == paymentId, ct);
        if (pago is null) return Result.Fail("Pago no encontrado.", "PAGO_NOT_FOUND");
        if (pago.Status != PendienteVerif) return Result.Fail("El pago no está pendiente de verificación.", "ESTADO_INVALIDO");

        var now = DateTime.UtcNow;
        pago.Status = "SUCCEEDED";
        pago.VerificadoPor = actor;
        pago.VerificadoAt = now;
        pago.PaidAt = now;

        var sub = pago.Subscription;
        sub.Status = SubscriptionStatus.Active;
        sub.CurrentPeriodEnd = now.AddMonths(1);
        await ActivarLicenciaAsync(sub.Customer.EmpresaId, sub.PlanId, sub.CurrentPeriodEnd, ct);
        await _db.SaveChangesAsync(ct);

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

    public async Task<Result> RechazarTransferenciaAsync(int paymentId, string motivo, string actor, CancellationToken ct = default)
    {
        var pago = await _db.BillingPayments
            .Include(p => p.Subscription).ThenInclude(s => s.Customer)
            .FirstOrDefaultAsync(p => p.Id == paymentId, ct);
        if (pago is null) return Result.Fail("Pago no encontrado.", "PAGO_NOT_FOUND");
        if (pago.Status != PendienteVerif) return Result.Fail("El pago no está pendiente de verificación.", "ESTADO_INVALIDO");

        pago.Status = "FAILED";
        pago.FailureReason = motivo;
        pago.VerificadoPor = actor;
        pago.VerificadoAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

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

    private async Task ActivarLicenciaAsync(int empresaId, int planId, DateTime? fin, CancellationToken ct)
    {
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
                FechaInicio = DateTime.UtcNow,
                FechaFin = fin,
                EstadoCodigo = "ACTIVO",
            });
        }
    }

    private static BillingSubscriptionDto MapSubscription(BillingSubscription s, Plan plan)
        => new(s.Id, s.BillingCustomerId, s.PlanId, plan.Nombre, s.Status, s.TrialEnd, s.CurrentPeriodEnd, s.CancelAtPeriodEnd);
}
