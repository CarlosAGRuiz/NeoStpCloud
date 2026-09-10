using Microsoft.Extensions.Options;
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
    private readonly BillingCheckoutOptions _checkoutOptions;
    private readonly IPaymentProviderResolver _payments;
    private readonly IPlanesService _planes;
    private readonly ICurrentUser _currentUser;
    private readonly IEmpresaContext _empresaContext;

    public BillingController(
        IBillingService billing,
        IPaymentProviderResolver payments,
        IPlanesService planes,
        ICurrentUser currentUser,
        IEmpresaContext empresaContext, IOptions<BillingOptions>? billingOptions = null)
    {
        _billing = billing;
        _checkoutOptions = billingOptions?.Value.Checkout ?? new();
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

        ViewBag.Metodos = _payments.Disponibles.Where(m => m == "Transferencia" || (_checkoutOptions.Enabled
            && m == _checkoutOptions.Provider && _payments.Resolve(m) is IBillingCheckoutProvider)).ToList();
        return View(planes.Value ?? Array.Empty<NeoSTP.Application.Licenciamiento.Dtos.PlanDto>());
    }

    [HttpPost("trial")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> StartTrial(int planId, CancellationToken ct)
    {
        if (RequireEmpresa() is not int empresaId) return RedirectToSoporte();
        if (!PuedeGestionar(empresaId)) return Forbid();

        var email = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value ?? string.Empty;
        var result = await _billing.StartTrialAsync(new StartTrialRequest(empresaId, planId, email), ct);
        if (result.ErrorCode == "BILLING_FORBIDDEN") return Forbid();

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
    public async Task<IActionResult> CreateCheckout(int planId, string? metodo, CancellationToken ct, string? idempotencyKey = null)
    {
        if (RequireEmpresa() is not int empresaId) return RedirectToSoporte();
        if (!PuedeGestionar(empresaId)) return Forbid();

        if (string.Equals(metodo, "Transferencia", StringComparison.OrdinalIgnoreCase))
        {
            // Creation occurs only on this antiforgery-protected POST, never by following a GET link.
            var transfer = await _billing.IniciarTransferenciaAsync(new IniciarTransferenciaRequest(empresaId, planId), ct);
            if (transfer.ErrorCode == "BILLING_FORBIDDEN") return Forbid();
            if (transfer.IsSuccess) return View(nameof(Transferencia), transfer.Value);
            TempData["Error"] = transfer.Error;
            return RedirectToAction(nameof(Checkout));
        }

        var returnUrl = _checkoutOptions.SuccessUrl;
        var result = await _billing.CreateCheckoutSessionAsync(new CreateCheckoutRequest(empresaId, planId, returnUrl, metodo, idempotencyKey), ct);
        if (result.Value?.CorrelationId is Guid correlation)
            return RedirectToAction(nameof(CheckoutStatus), new { correlationId = correlation });
        if (result.ErrorCode == "BILLING_FORBIDDEN") return Forbid();

        if (!result.IsSuccess)
        {
            TempData["Error"] = result.Error;
            return RedirectToAction(nameof(Checkout));
        }

        return RedirectToAction(nameof(CheckoutStatus), new { correlationId = result.Value!.CorrelationId });
    }

    [HttpGet("checkouts/{correlationId:guid}")]
    public async Task<IActionResult> CheckoutStatus(Guid correlationId, CancellationToken ct)
    {
        if (RequireEmpresa() is not int empresaId) return RedirectToSoporte();
        var result = await _billing.GetCheckoutAsync(empresaId, correlationId, ct);
        if (result.ErrorCode == "BILLING_FORBIDDEN") return Forbid();
        if (!result.IsSuccess) return NotFound();
        return View(result.Value);
    }
    [HttpGet("transferencia")]
    public IActionResult Transferencia(int planId, CancellationToken ct)
    {
        if (RequireEmpresa() is not int empresaId) return RedirectToSoporte();
        if (!PuedeGestionar(empresaId)) return Forbid();
        return RedirectToAction(nameof(Checkout));
    }

    [HttpPost("transferencia/comprobante")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SubirComprobante(int paymentId, string comprobante, CancellationToken ct)
    {
        if (RequireEmpresa() is not int empresaId) return RedirectToSoporte();
        if (!PuedeGestionar(empresaId)) return Forbid();

        var result = await _billing.RegistrarComprobanteAsync(empresaId, paymentId, comprobante ?? string.Empty, ct);
        if (result.ErrorCode == "BILLING_FORBIDDEN") return Forbid();
        TempData[result.IsSuccess ? "Success" : "Error"] = result.IsSuccess
            ? "Comprobante registrado. Un administrador verificara tu pago."
            : result.Error;
        return RedirectToAction(nameof(Portal));
    }

    [HttpGet("transferencias")]
    public async Task<IActionResult> Transferencias(CancellationToken ct)
    {
        if (!EsAdminCentral()) return Forbid();

        var result = await _billing.GetTransferenciasPendientesAsync(null, ct);
        if (result.ErrorCode == "BILLING_FORBIDDEN") return Forbid();
        return View(result.Value ?? new List<TransferenciaPendienteDto>());
    }

    [HttpPost("transferencias/{paymentId:int}/confirmar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ConfirmarTransferencia(int paymentId, CancellationToken ct)
    {
        if (!EsAdminCentral()) return Forbid();

        var result = await _billing.ConfirmarTransferenciaAsync(paymentId, _currentUser.Username ?? "admin", ct);
        if (result.ErrorCode == "BILLING_FORBIDDEN") return Forbid();
        TempData[result.IsSuccess ? "Success" : "Error"] = result.IsSuccess
            ? "Transferencia confirmada y suscripcion activada."
            : result.Error;
        return RedirectToAction(nameof(Transferencias));
    }

    [HttpPost("transferencias/{paymentId:int}/rechazar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RechazarTransferencia(int paymentId, string motivo, CancellationToken ct)
    {
        if (!EsAdminCentral()) return Forbid();

        var result = await _billing.RechazarTransferenciaAsync(paymentId, motivo ?? "Sin motivo", _currentUser.Username ?? "admin", ct);
        if (result.ErrorCode == "BILLING_FORBIDDEN") return Forbid();
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
        if (sub.IsSuccess && sub.Value?.CalendarBilling == true)
            return RedirectToAction(nameof(Index));
        var payments = await _billing.GetPaymentsAsync(empresaId, ct);
        var invoices = await _billing.GetInvoicesAsync(empresaId, ct);
        var planes = await _planes.GetListAsync(ct);

        ViewBag.Subscription = sub.Value;
        ViewBag.Payments = payments.Value ?? new List<BillingPaymentDto>();
        ViewBag.Invoices = invoices.Value ?? new List<BillingInvoiceDto>();
        ViewBag.Planes = planes.Value ?? Array.Empty<NeoSTP.Application.Licenciamiento.Dtos.PlanDto>();
        return View();
    }

    [HttpPost("portal/external")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> OpenExternalPortal(CancellationToken ct)
    {
        if (RequireEmpresa() is not int empresaId) return RedirectToSoporte();
        if (!PuedeGestionar(empresaId)) return Forbid();

        var result = await _billing.GetPortalUrlAsync(empresaId, ct);
        if (result.ErrorCode == "BILLING_FORBIDDEN") return Forbid();

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
        if (!PuedeGestionar(empresaId)) return Forbid();

        var result = await _billing.ChangePlanAsync(new ChangePlanRequest(empresaId, newPlanId), ct);
        if (result.ErrorCode == "BILLING_FORBIDDEN") return Forbid();

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
        if (!PuedeGestionar(empresaId)) return Forbid();

        var result = await _billing.CancelSubscriptionAsync(new CancelSubscriptionRequest(empresaId, atPeriodEnd), ct);
        if (result.ErrorCode == "BILLING_FORBIDDEN") return Forbid();

        TempData[result.IsSuccess ? "Success" : "Error"] = result.IsSuccess
            ? "Suscripcion cancelada."
            : result.Error;

        return RedirectToAction(nameof(Portal));
    }

    private int? RequireEmpresa() => _empresaContext.CurrentEmpresaId;

    private bool EsAdminCentral()
        => _currentUser.IsAuthenticated && _currentUser.UserId is > 0
            && _currentUser.EmpresaId is null && _currentUser.TipoUsuarioCodigo == "SUPERADMIN"
            && _currentUser.IsInRole("SUPERADMIN");

    private bool PuedeGestionar(int empresaId)
        => EsAdminCentral() || (_currentUser.IsAuthenticated && _currentUser.EmpresaId == empresaId
            && (_currentUser.TipoUsuarioCodigo == "ADMIN" || _currentUser.IsInRole("ADMIN")));

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
