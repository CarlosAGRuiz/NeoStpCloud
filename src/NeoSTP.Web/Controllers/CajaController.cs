using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Common;
using NeoSTP.Application.Empresas;
using NeoSTP.Application.Pos;
using NeoSTP.Application.Pos.Dtos;

namespace NeoSTP.Web.Controllers;

/// <summary>NEOPOS — sesiones / corte de caja. Permisos Pos.Ver / Pos.Vender.</summary>
[Authorize]
public class CajaController : Controller
{
    private readonly IPosCajaService _caja;
    private readonly ICurrentUser _currentUser;
    private readonly IEmpresaContext _empresaContext;

    public CajaController(IPosCajaService caja, ICurrentUser currentUser, IEmpresaContext empresaContext)
    {
        _caja = caja;
        _currentUser = currentUser;
        _empresaContext = empresaContext;
    }

    [HttpGet]
    public async Task<IActionResult> Index(int page = 1, CancellationToken ct = default)
    {
        if (!Has("Pos.Ver")) return Forbid();
        if (RequireEmpresa() is not int eid) return RedirectToSoporte();

        var estado = await _caja.GetEstadoAsync(eid, ct);
        var historial = await _caja.ListAsync(eid, new PagedQuery { Page = page, PageSize = 20 }, ct);
        ViewBag.Historial = historial.Value;
        ViewBag.PuedeGestionar = Has("Pos.Vender");
        return View(estado.Value);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Abrir(AbrirCajaRequest model, CancellationToken ct)
    {
        if (!Has("Pos.Vender")) return Forbid();
        if (RequireEmpresa() is not int eid) return RedirectToSoporte();

        var r = await _caja.AbrirAsync(eid, model, _currentUser.Username, ct);
        TempData[r.IsSuccess ? "Success" : "Error"] = r.IsSuccess ? $"Caja {r.Value!.Numero} abierta." : r.Error;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cerrar(int id, CerrarCajaRequest model, CancellationToken ct)
    {
        if (!Has("Pos.Vender")) return Forbid();
        if (RequireEmpresa() is not int eid) return RedirectToSoporte();

        var r = await _caja.CerrarAsync(eid, id, model, _currentUser.Username, ct);
        TempData[r.IsSuccess ? "Success" : "Error"] = r.IsSuccess
            ? $"Caja cerrada. Diferencia: {r.Value!.Diferencia:N2}."
            : r.Error;
        return RedirectToAction(nameof(Index));
    }

    private bool Has(string codigo) => _currentUser.TipoUsuarioCodigo == "SUPERADMIN" || _currentUser.HasPermiso(codigo);
    private int? RequireEmpresa() => _empresaContext.CurrentEmpresaId;

    private IActionResult RedirectToSoporte()
    {
        if (_currentUser.TipoUsuarioCodigo == "SUPERADMIN")
        {
            TempData["Error"] = "La caja opera dentro de una empresa. Selecciona una en modo soporte primero.";
            return RedirectToAction("Index", "Soporte");
        }
        return RedirectToAction("Index", "Home");
    }
}
