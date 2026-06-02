using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Billing;
using NeoSTP.Application.Billing.Dtos;
using NeoSTP.Application.Empresas;
using NeoSTP.Application.Licenciamiento;

namespace NeoSTP.Web.Controllers;

[Authorize]
[Route("billing")]
public class BillingController : Controller
{
    private readonly IBillingService _billing;
    private readonly IPaymentProviderResolver _payments;
    private readonly IPlanesService _planes;
    private readonly ICurrentUser _currentUser;
    private readonly IEmpresaContext _empresaContext;

    public BillingController(
        IBillingService billing,
        IPaymentProviderResolver payments,
        IPlanesService planes,
        ICurrentUser currentUser,
        IEmpresaContext empresaContext)
    {
        _billing = billing;
        _payments = payments;
        _planes = planes;
        _currentUser = currentUser;
        _empresaContext = empresaContext;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        if (RequireEmpresa() is not int empresaId) return RedirectToSoporte();

        var sub = await _billing.GetActiveSubscriptionAsync(empresaId, ct);
        var payments = await _billing.GetPaymentsAsync(empresaId, ct);
        var invoices = await _billing.GetInvoicesAsync(empresaId, ct);

        ViewBag.Subscription = sub.Value;
        ViewBag.Payments = payments.Value ?? new List<BillingPaymentDto>();
        ViewBag.Invoices = invoices.Value ?? new List<BillingInvoiceDto>();
        return View();
    }

    [HttpGet("checkout")]
    public async Task<IActionResult> Checkout(CancellationToken ct)
    {
        if (RequireEmpresa() is not int) return RedirectToSoporte();

        var planes = await _planes.GetListAsync(ct);
        if (!planes.IsSuccess)
        {
            TempData["Error"] = planes.Error ?? "No se pudieron cargar los planes disponibles.";
        }

        ViewBag.Metodos = _payments.Disponibles;
        return View(planes.Value ?? Array.Empty<NeoSTP.Application.Licenciamiento.Dtos.PlanDto>());
    }

    [HttpPost("trial")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> StartTrial(int planId, CancellationToken ct)
    {
        if (RequireEmpresa() is not int empresaId) return RedirectToSoporte();

        var email = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value ?? string.Empty;
        var result = await _billing.StartTrialAsync(new StartTrialRequest(empresaId, planId, email), ct);

        if (!result.IsSuccess)
        {
            TempData["Error"] = result.Error;
            return RedirectToAction(nameof(Checkout));
        }

        TempData["Success"] = $"Tu trial de {result.Value!.PlanNombre} ha iniciado. Vence el {result.Value.TrialEnd:dd/MM/yyyy}.";
        return RedirectToAction(nameof(Portal));
    }

    [HttpPost("checkout/session")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateCheckout(int planId, string? metodo, CancellationToken ct)
    {
        if (RequireEmpresa() is not int empresaId) return RedirectToSoporte();

        if (string.Equals(metodo, "Transferencia", StringComparison.OrdinalIgnoreCase))
            return RedirectToAction(nameof(Transferencia), new { planId });

        var returnUrl = Url.Action(nameof(Portal), "Billing", null, Request.Scheme)!;
        var result = await _billing.CreateCheckoutSessionAsync(new CreateCheckoutRequest(empresaId, planId, returnUrl, metodo), ct);

        if (!result.IsSuccess)
        {
            TempData["Error"] = result.Error;
            return RedirectToAction(nameof(Checkout));
        }

        return Redirect(result.Value!.RedirectUrl);
    }

    [HttpGet("transferencia")]
    public async Task<IActionResult> Transferencia(int planId, CancellationToken ct)
    {
        if (RequireEmpresa() is not int empresaId) return RedirectToSoporte();

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
        if (RequireEmpresa() is not int empresaId) return RedirectToSoporte();

        var result = await _billing.RegistrarComprobanteAsync(empresaId, paymentId, comprobante ?? string.Empty, ct);
        TempData[result.IsSuccess ? "Success" : "Error"] = result.IsSuccess
            ? "Comprobante registrado. Un administrador verificara tu pago."
            : result.Error;
        return RedirectToAction(nameof(Portal));
    }

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

        var result = await _billing.ConfirmarTransferenciaAsync(paymentId, _currentUser.Username ?? "admin", ct);
        TempData[result.IsSuccess ? "Success" : "Error"] = result.IsSuccess
            ? "Transferencia confirmada y suscripcion activada."
            : result.Error;
        return RedirectToAction(nameof(Transferencias));
    }

    [HttpPost("transferencias/{paymentId:int}/rechazar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RechazarTransferencia(int paymentId, string motivo, CancellationToken ct)
    {
        if (!EsAdmin()) return Forbid();

        var result = await _billing.RechazarTransferenciaAsync(paymentId, motivo ?? "Sin motivo", _currentUser.Username ?? "admin", ct);
        TempData[result.IsSuccess ? "Success" : "Error"] = result.IsSuccess
            ? "Transferencia rechazada."
            : result.Error;
        return RedirectToAction(nameof(Transferencias));
    }

    [HttpGet("portal")]
    public async Task<IActionResult> Portal(CancellationToken ct)
    {
        if (RequireEmpresa() is not int empresaId) return RedirectToSoporte();

        var sub = await _billing.GetActiveSubscriptionAsync(empresaId, ct);
        var payments = await _billing.GetPaymentsAsync(empresaId, ct);
        var invoices = await _billing.GetInvoicesAsync(empresaId, ct);

        ViewBag.Subscription = sub.Value;
        ViewBag.Payments = payments.Value ?? new List<BillingPaymentDto>();
        ViewBag.Invoices = invoices.Value ?? new List<BillingInvoiceDto>();
        return View();
    }

    [HttpPost("portal/external")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> OpenExternalPortal(CancellationToken ct)
    {
        if (RequireEmpresa() is not int empresaId) return RedirectToSoporte();

        var result = await _billing.GetPortalUrlAsync(empresaId, ct);

        if (!result.IsSuccess)
        {
            TempData["Error"] = result.Error;
            return RedirectToAction(nameof(Portal));
        }

        return Redirect(result.Value!.PortalUrl);
    }

    [HttpPost("change-plan")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePlan(int newPlanId, CancellationToken ct)
    {
        if (RequireEmpresa() is not int empresaId) return RedirectToSoporte();

        var result = await _billing.ChangePlanAsync(new ChangePlanRequest(empresaId, newPlanId), ct);

        TempData[result.IsSuccess ? "Success" : "Error"] = result.IsSuccess
            ? "Plan actualizado correctamente."
            : result.Error;

        return RedirectToAction(nameof(Portal));
    }

    [HttpPost("cancel")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(bool atPeriodEnd, CancellationToken ct)
    {
        if (RequireEmpresa() is not int empresaId) return RedirectToSoporte();

        var result = await _billing.CancelSubscriptionAsync(new CancelSubscriptionRequest(empresaId, atPeriodEnd), ct);

        TempData[result.IsSuccess ? "Success" : "Error"] = result.IsSuccess
            ? "Suscripcion cancelada."
            : result.Error;

        return RedirectToAction(nameof(Portal));
    }

    private int? RequireEmpresa() => _empresaContext.CurrentEmpresaId;

    private bool EsAdmin()
        => _currentUser.TipoUsuarioCodigo is "SUPERADMIN" or "ADMIN";

    private IActionResult RedirectToSoporte()
    {
        if (_currentUser.TipoUsuarioCodigo == "SUPERADMIN")
        {
            TempData["Error"] = "Billing opera dentro de una empresa. Selecciona una en modo soporte primero.";
            return RedirectToAction("Index", "Soporte");
        }

        TempData["Error"] = "No se pudo resolver la empresa activa para Billing.";
        return RedirectToAction("Index", "Home");
    }
}
