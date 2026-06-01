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

    public BillingController(IBillingService billing)
    {
        _billing = billing;
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
    public async Task<IActionResult> CreateCheckout(int planId, CancellationToken ct)
    {
        var empresaId  = ObtenerEmpresaId();
        var returnUrl  = Url.Action(nameof(Portal), "Billing", null, Request.Scheme)!;
        var result     = await _billing.CreateCheckoutSessionAsync(new CreateCheckoutRequest(empresaId, planId, returnUrl), ct);

        if (!result.IsSuccess)
        {
            TempData["Error"] = result.Error;
            return RedirectToAction(nameof(Checkout));
        }

        return Redirect(result.Value!.RedirectUrl);
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
}
