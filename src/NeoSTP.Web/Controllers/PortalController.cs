using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NeoSTP.Application.Portal;

namespace NeoSTP.Web.Controllers;

/// <summary>
/// NEOPORTAL — portal PÚBLICO del receptor (sin sesión). Resuelve enlaces por token
/// (expirables/revocables). Todo el aislamiento por empresa/cliente lo garantiza
/// <see cref="IPortalService"/>: el token no puede cruzar empresa ni documento.
/// </summary>
[AllowAnonymous]
[Route("portal")]
public class PortalController : Controller
{
    private readonly IPortalService _portal;

    public PortalController(IPortalService portal)
    {
        _portal = portal;
    }

    [HttpGet("{token}")]
    public async Task<IActionResult> Index(string token, CancellationToken ct)
    {
        // Documento primero; si el token es de estado de cuenta, resuelve la otra vista.
        var doc = await _portal.GetDocumentoAsync(token, ct);
        if (doc.IsSuccess)
        {
            ViewBag.Token = token;
            return View("Documento", doc.Value);
        }
        if (doc.ErrorCode is "TOKEN_EXPIRADO" or "TOKEN_REVOCADO")
            return NoDisponible(doc.Error!);

        var cuenta = await _portal.GetEstadoCuentaAsync(token, ct);
        if (cuenta.IsSuccess)
        {
            ViewBag.Token = token;
            return View("EstadoCuenta", cuenta.Value);
        }
        return NoDisponible(cuenta.Error ?? "Enlace inválido.");
    }

    [HttpGet("{token}/pdf")]
    public async Task<IActionResult> Pdf(string token, CancellationToken ct)
    {
        var r = await _portal.GetArchivosAsync(token, ct);
        if (r.IsFailure) return NoDisponible(r.Error!);
        return File(r.Value!.PdfContent, "application/pdf", r.Value.PdfFileName);
    }

    [HttpGet("{token}/json")]
    public async Task<IActionResult> Json(string token, CancellationToken ct)
    {
        var r = await _portal.GetArchivosAsync(token, ct);
        if (r.IsFailure) return NoDisponible(r.Error!);
        return File(System.Text.Encoding.UTF8.GetBytes(r.Value!.JsonContent), "application/json", r.Value.JsonFileName);
    }

    [HttpGet("{token}/qr")]
    public async Task<IActionResult> Qr(string token, int? dteId, CancellationToken ct)
    {
        var r = await _portal.GetQrPagoAsync(token, dteId, ct);
        if (r.IsFailure) return NoDisponible(r.Error!);
        ViewBag.Token = token;
        return View("Qr", r.Value);
    }

    [HttpPost("{token}/reenviar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reenviar(string token, string? destinatario, CancellationToken ct)
    {
        var r = await _portal.ReenviarCorreoAsync(token, destinatario, ct);
        TempData[r.IsSuccess ? "PortalOk" : "PortalError"] = r.IsSuccess
            ? "Documento reenviado por correo."
            : r.Error;
        return RedirectToAction(nameof(Index), new { token });
    }

    private IActionResult NoDisponible(string mensaje)
    {
        ViewBag.Mensaje = mensaje;
        Response.StatusCode = 404;
        return View("NoDisponible");
    }
}
