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
    private readonly IPaymentProvider _payment;
    private readonly IEmailSender _email;
    private readonly BillingOptions _options;

    public BillingService(
        NeoStpDbContext db,
        IPaymentProvider payment,
        IEmailSender email,
        IOptions<BillingOptions> options)
    {
        _db = db;
        _payment = payment;
        _email = email;
        _options = options.Value;
    }

    // ─── Trial ────────────────────────────────────────────────────────────────

    public async Task<Result<BillingSubscriptionDto>> StartTrialAsync(StartTrialRequest request, CancellationToken ct = default)
    {
        var existing = await ActiveSubscriptionQuery(request.EmpresaId).FirstOrDefaultAsync(ct);
        if (existing != null)
            return Result<BillingSubscriptionDto>.Fail("La empresa ya tiene una suscripción activa o en trial.");

        var plan = await _db.Planes.FindAsync(new object[] { request.PlanId }, ct);
        if (plan is null)
            return Result<BillingSubscriptionDto>.Fail("Plan no encontrado.");

        var customer = await GetOrCreateCustomerAsync(request.EmpresaId, request.Email, ct);
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
        var customer = await GetOrCreateCustomerAsync(request.EmpresaId, string.Empty, ct);
        if (!customer.IsSuccess) return Result<CheckoutSessionResult>.Fail(customer.Error!);

        var mapping = await _db.BillingPlanProviderMappings
            .Where(m => m.PlanId == request.PlanId && m.Provider == _options.Provider && m.IsActive)
            .FirstOrDefaultAsync(ct);

        var externalPlanId = mapping?.ExternalPlanId ?? $"mock_price_{request.PlanId}";

        return await _payment.CreateCheckoutSessionAsync(
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

        return await _payment.CreatePortalSessionAsync(
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
                .Where(m => m.PlanId == request.NewPlanId && m.Provider == _options.Provider && m.IsActive)
                .FirstOrDefaultAsync(ct);
            var externalPlanId = mapping?.ExternalPlanId ?? $"mock_price_{request.NewPlanId}";
            var changeResult = await _payment.ChangePlanAsync(sub.ExternalSubscriptionId, externalPlanId, ct);
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
            var cancelResult = await _payment.CancelSubscriptionAsync(sub.ExternalSubscriptionId, request.AtPeriodEnd, ct);
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

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private IQueryable<BillingSubscription> ActiveSubscriptionQuery(int empresaId)
        => _db.BillingSubscriptions
              .Include(s => s.Customer)
              .Where(s => s.Customer.EmpresaId == empresaId
                       && s.Status != SubscriptionStatus.Canceled
                       && s.Status != SubscriptionStatus.Expired);

    private async Task<Result<BillingCustomer>> GetOrCreateCustomerAsync(int empresaId, string email, CancellationToken ct)
    {
        var customer = await _db.BillingCustomers.FirstOrDefaultAsync(c => c.EmpresaId == empresaId, ct);
        if (customer != null)
            return Result<BillingCustomer>.Ok(customer);

        var extResult = await _payment.CreateCustomerAsync(email, empresaId, ct);
        if (!extResult.IsSuccess)
            return Result<BillingCustomer>.Fail(extResult.Error!);

        customer = new BillingCustomer
        {
            EmpresaId = empresaId,
            Email = email,
            Provider = _options.Provider,
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
