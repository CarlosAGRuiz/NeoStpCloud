using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NeoSTP.Web.Auth;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Clientes;
using NeoSTP.Application.Common;
using NeoSTP.Application.Empresas;
using NeoSTP.Application.Portal;

namespace NeoSTP.Web.Controllers;

/// <summary>NEOPORTAL â€” gestiÃ³n interna de enlaces pÃºblicos. Permisos Portal.Enlaces.*.</summary>
[Authorize]
[RequireModulo("NEOPORTAL")]
public class PortalEnlacesController : Controller
{
    private readonly IPortalService _portal;
    private readonly IClientesService _clientes;
    private readonly ICurrentUser _currentUser;
    private readonly IEmpresaContext _empresaContext;

    public PortalEnlacesController(IPortalService portal, IClientesService clientes, ICurrentUser currentUser, IEmpresaContext empresaContext)
    {
        _portal = portal;
        _clientes = clientes;
        _currentUser = currentUser;
        _empresaContext = empresaContext;
    }

    [HttpGet]
    public async Task<IActionResult> Index(int page = 1, CancellationToken ct = default)
    {
        if (!Has("Portal.Enlaces.Ver")) return Forbid();
        if (RequireEmpresa() is not int eid) return RedirectToSoporte();

        var enlaces = await _portal.ListEnlacesAsync(eid, new PagedQuery { Page = page, PageSize = 20 }, ct);
        var clientes = await _clientes.GetListAsync(eid, new PagedQuery { Page = 1, PageSize = 1000 }, ct);
        ViewBag.Clientes = clientes.Value?.Items ?? new List<NeoSTP.Application.Clientes.Dtos.ClienteDto>();
        ViewBag.PuedeGestionar = Has("Portal.Enlaces.Gestionar");
        return View(enlaces.Value);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GenerarDocumento(int dteDocumentoId, int diasValidez, string? nota, CancellationToken ct)
    {
        if (!Has("Portal.Enlaces.Gestionar")) return Forbid();
        if (RequireEmpresa() is not int eid) return RedirectToSoporte();

        var r = await _portal.GenerarEnlaceDocumentoAsync(eid, dteDocumentoId,
            new GenerarEnlacePortalRequest { DiasValidez = diasValidez, Nota = nota }, _currentUser.Username, ct);
        SetResultado(r.IsSuccess, r.IsSuccess ? r.Value!.Token : null, r.Error);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GenerarEstadoCuenta(int clienteId, int diasValidez, string? nota, CancellationToken ct)
    {
        if (!Has("Portal.Enlaces.Gestionar")) return Forbid();
        if (RequireEmpresa() is not int eid) return RedirectToSoporte();

        var r = await _portal.GenerarEnlaceEstadoCuentaAsync(eid, clienteId,
            new GenerarEnlacePortalRequest { DiasValidez = diasValidez, Nota = nota }, _currentUser.Username, ct);
        SetResultado(r.IsSuccess, r.IsSuccess ? r.Value!.Token : null, r.Error);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Revocar(int id, CancellationToken ct)
    {
        if (!Has("Portal.Enlaces.Gestionar")) return Forbid();
        if (RequireEmpresa() is not int eid) return RedirectToSoporte();
        var r = await _portal.RevocarAsync(eid, id, _currentUser.Username, ct);
        TempData[r.IsSuccess ? "Success" : "Error"] = r.IsSuccess ? "Enlace revocado." : r.Error;
        return RedirectToAction(nameof(Index));
    }

    private void SetResultado(bool ok, string? token, string? error)
    {
        if (ok && token is not null)
        {
            // El token solo se muestra una vez: se compone la URL pÃºblica completa.
            TempData["EnlaceGenerado"] = $"{Request.Scheme}://{Request.Host}/portal/{token}";
            TempData["Success"] = "Enlace generado. CÃ³pialo ahora: no volverÃ¡ a mostrarse.";
        }
        else
        {
            TempData["Error"] = error ?? "No se pudo generar el enlace.";
        }
    }

    private bool Has(string codigo) => _currentUser.TipoUsuarioCodigo == "SUPERADMIN" || _currentUser.HasPermiso(codigo);
    private int? RequireEmpresa() => _empresaContext.CurrentEmpresaId;

    private IActionResult RedirectToSoporte()
    {
        if (_currentUser.TipoUsuarioCodigo == "SUPERADMIN")
        {
            TempData["Error"] = "El portal opera dentro de una empresa. Selecciona una en modo soporte primero.";
            return RedirectToAction("Index", "Soporte");
        }
        return RedirectToAction("Index", "Home");
    }
}
