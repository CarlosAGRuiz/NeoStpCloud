using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NeoSTP.Application.Billing;
using NeoSTP.Application.Billing.Dtos;

namespace NeoSTP.Web.Controllers;

[Authorize]
[Route("billing")]
public class BillingController : Controller
{
    private readonly IBillingService _billing;
    private readonly IPaymentProviderResolver _payments;

    public BillingController(IBillingService billing, IPaymentProviderResolver payments)
    {
        _billing = billing;
        _payments = payments;
    }

    // ─── Index: resumen de suscripción ────────────────────────────────────

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var empresaId = ObtenerEmpresaId();
        if (empresaId == 0) return RedirectToAction("Index", "Home");

        var sub      = await _billing.GetActiveSubscriptionAsync(empresaId, ct);
        var payments = await _billing.GetPaymentsAsync(empresaId, ct);
        var invoices = await _billing.GetInvoicesAsync(empresaId, ct);

        ViewBag.Subscription = sub.Value;
        ViewBag.Payments     = payments.Value ?? new List<BillingPaymentDto>();
        ViewBag.Invoices     = invoices.Value ?? new List<BillingInvoiceDto>();
        return View();
    }

    // ─── Checkout ─────────────────────────────────────────────────────────

    [HttpGet("checkout")]
    public IActionResult Checkout()
    {
        ViewBag.Metodos = _payments.Disponibles;
        return View();
    }

    [HttpPost("trial")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> StartTrial(int planId, CancellationToken ct)
    {
        var empresaId = ObtenerEmpresaId();
        var email     = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value ?? string.Empty;

        var result = await _billing.StartTrialAsync(new StartTrialRequest(empresaId, planId, email), ct);

        if (!result.IsSuccess)
        {
            TempData["Error"] = result.Error;
            return RedirectToAction(nameof(Checkout));
        }

        TempData["Success"] = $"¡Tu trial de {result.Value!.PlanNombre} ha iniciado! Vence el {result.Value.TrialEnd:dd/MM/yyyy}.";
        return RedirectToAction(nameof(Portal));
    }

    [HttpPost("checkout/session")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateCheckout(int planId, string? metodo, CancellationToken ct)
    {
        // Transferencia es offline: va a su propia página de instrucciones.
        if (string.Equals(metodo, "Transferencia", StringComparison.OrdinalIgnoreCase))
            return RedirectToAction(nameof(Transferencia), new { planId });

        var empresaId  = ObtenerEmpresaId();
        var returnUrl  = Url.Action(nameof(Portal), "Billing", null, Request.Scheme)!;
        var result     = await _billing.CreateCheckoutSessionAsync(new CreateCheckoutRequest(empresaId, planId, returnUrl, metodo), ct);

        if (!result.IsSuccess)
        {
            TempData["Error"] = result.Error;
            return RedirectToAction(nameof(Checkout));
        }

        return Redirect(result.Value!.RedirectUrl);
    }

    // ─── Transferencia bancaria (offline) ───────────────────────────────────

    [HttpGet("transferencia")]
    public async Task<IActionResult> Transferencia(int planId, CancellationToken ct)
    {
        var empresaId = ObtenerEmpresaId();
        var result = await _billing.IniciarTransferenciaAsync(new IniciarTransferenciaRequest(empresaId, planId), ct);
        if (!result.IsSuccess)
        {
            TempData["Error"] = result.Error;
            return RedirectToAction(nameof(Checkout));
        }
        return View(result.Value);
    }

    [HttpPost("transferencia/comprobante")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SubirComprobante(int paymentId, string comprobante, CancellationToken ct)
    {
        var empresaId = ObtenerEmpresaId();
        var result = await _billing.RegistrarComprobanteAsync(empresaId, paymentId, comprobante ?? string.Empty, ct);
        TempData[result.IsSuccess ? "Success" : "Error"] = result.IsSuccess
            ? "Comprobante registrado. Un administrador verificará tu pago."
            : result.Error;
        return RedirectToAction(nameof(Portal));
    }

    // ─── Bandeja admin de verificación (SuperAdmin) ─────────────────────────

    [HttpGet("transferencias")]
    public async Task<IActionResult> Transferencias(CancellationToken ct)
    {
        if (!EsAdmin()) return Forbid();
        var result = await _billing.GetTransferenciasPendientesAsync(null, ct);
        return View(result.Value ?? new List<TransferenciaPendienteDto>());
    }

    [HttpPost("transferencias/{paymentId:int}/confirmar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ConfirmarTransferencia(int paymentId, CancellationToken ct)
    {
        if (!EsAdmin()) return Forbid();
        var result = await _billing.ConfirmarTransferenciaAsync(paymentId, User.Identity?.Name ?? "admin", ct);
        TempData[result.IsSuccess ? "Success" : "Error"] = result.IsSuccess ? "Transferencia confirmada y suscripción activada." : result.Error;
        return RedirectToAction(nameof(Transferencias));
    }

    [HttpPost("transferencias/{paymentId:int}/rechazar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RechazarTransferencia(int paymentId, string motivo, CancellationToken ct)
    {
        if (!EsAdmin()) return Forbid();
        var result = await _billing.RechazarTransferenciaAsync(paymentId, motivo ?? "Sin motivo", User.Identity?.Name ?? "admin", ct);
        TempData[result.IsSuccess ? "Success" : "Error"] = result.IsSuccess ? "Transferencia rechazada." : result.Error;
        return RedirectToAction(nameof(Transferencias));
    }

    // ─── Portal de facturación ────────────────────────────────────────────

    [HttpGet("portal")]
    public async Task<IActionResult> Portal(CancellationToken ct)
    {
        var empresaId = ObtenerEmpresaId();
        var sub       = await _billing.GetActiveSubscriptionAsync(empresaId, ct);
        var payments  = await _billing.GetPaymentsAsync(empresaId, ct);
        var invoices  = await _billing.GetInvoicesAsync(empresaId, ct);

        ViewBag.Subscription = sub.Value;
        ViewBag.Payments     = payments.Value ?? new List<BillingPaymentDto>();
        ViewBag.Invoices     = invoices.Value ?? new List<BillingInvoiceDto>();
        return View();
    }

    [HttpPost("portal/external")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> OpenExternalPortal(CancellationToken ct)
    {
        var empresaId = ObtenerEmpresaId();
        var returnUrl = Url.Action(nameof(Portal), "Billing", null, Request.Scheme)!;
        var result    = await _billing.GetPortalUrlAsync(empresaId, ct);

        if (!result.IsSuccess)
        {
            TempData["Error"] = result.Error;
            return RedirectToAction(nameof(Portal));
        }

        return Redirect(result.Value!.PortalUrl);
    }

    // ─── Cambio de plan ───────────────────────────────────────────────────

    [HttpPost("change-plan")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePlan(int newPlanId, CancellationToken ct)
    {
        var empresaId = ObtenerEmpresaId();
        var result    = await _billing.ChangePlanAsync(new ChangePlanRequest(empresaId, newPlanId), ct);

        TempData[result.IsSuccess ? "Success" : "Error"] = result.IsSuccess
            ? "Plan actualizado correctamente."
            : result.Error;

        return RedirectToAction(nameof(Portal));
    }

    // ─── Cancelación ──────────────────────────────────────────────────────

    [HttpPost("cancel")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(bool atPeriodEnd, CancellationToken ct)
    {
        var empresaId = ObtenerEmpresaId();
        var result    = await _billing.CancelSubscriptionAsync(new CancelSubscriptionRequest(empresaId, atPeriodEnd), ct);

        TempData[result.IsSuccess ? "Success" : "Error"] = result.IsSuccess
            ? "Suscripción cancelada."
            : result.Error;

        return RedirectToAction(nameof(Portal));
    }

    // ─── Helper ───────────────────────────────────────────────────────────

    private int ObtenerEmpresaId()
    {
        var claim = User.FindFirst("empresaId")?.Value
                 ?? User.FindFirst("EmpresaId")?.Value;
        return int.TryParse(claim, out var id) ? id : 0;
    }

    private bool EsAdmin()
    {
        var tipo = User.FindFirst(NeoSTP.Web.Auth.CookieCurrentUser.ClaimTipoUsuario)?.Value;
        return tipo is "SUPERADMIN" or "ADMIN";
    }
}
