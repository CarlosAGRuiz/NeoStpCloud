using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Common;
using NeoSTP.Application.Empresas;
using NeoSTP.Application.Inventario;
using NeoSTP.Application.Inventario.Dtos;

namespace NeoSTP.Web.Controllers;

/// <summary>INVENTARIO — existencias y kardex. Permisos Inventario.Ver / Inventario.Gestionar.</summary>
[Authorize]
public class InventarioController : Controller
{
    private readonly IInventarioService _inv;
    private readonly ICurrentUser _currentUser;
    private readonly IEmpresaContext _empresaContext;

    public InventarioController(IInventarioService inv, ICurrentUser currentUser, IEmpresaContext empresaContext)
    {
        _inv = inv;
        _currentUser = currentUser;
        _empresaContext = empresaContext;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? search, bool soloStockBajo = false, int page = 1, CancellationToken ct = default)
    {
        if (!Has("Inventario.Ver")) return Forbid();
        if (RequireEmpresa() is not int eid) return RedirectToSoporte();

        var result = await _inv.ListExistenciasAsync(eid, soloStockBajo, new PagedQuery { Search = search, Page = page, PageSize = 20 }, ct);
        var resumen = await _inv.ResumenAsync(eid, ct);
        ViewBag.Search = search;
        ViewBag.SoloStockBajo = soloStockBajo;
        ViewBag.Resumen = resumen.Value;
        ViewBag.PuedeGestionar = Has("Inventario.Gestionar");
        return View(result.Value);
    }

    [HttpGet]
    public async Task<IActionResult> Kardex(int productoId, int page = 1, CancellationToken ct = default)
    {
        if (!Has("Inventario.Ver")) return Forbid();
        if (RequireEmpresa() is not int eid) return RedirectToSoporte();

        var existencia = await _inv.GetExistenciaAsync(eid, productoId, ct);
        if (existencia.IsFailure) return NotFound();
        var kardex = await _inv.GetKardexAsync(eid, productoId, new PagedQuery { Page = page, PageSize = 30 }, ct);
        ViewBag.Existencia = existencia.Value;
        ViewBag.PuedeGestionar = Has("Inventario.Gestionar");
        return View(kardex.Value);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Entrada(RegistrarMovimientoInventarioRequest model, CancellationToken ct)
    {
        if (!Has("Inventario.Gestionar")) return Forbid();
        if (RequireEmpresa() is not int eid) return RedirectToSoporte();
        var r = await _inv.RegistrarEntradaAsync(eid, model, _currentUser.Username, ct);
        TempData[r.IsSuccess ? "Success" : "Error"] = r.IsSuccess ? "Entrada registrada." : r.Error;
        return RedirectToAction(nameof(Kardex), new { productoId = model.ProductoId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Salida(RegistrarMovimientoInventarioRequest model, CancellationToken ct)
    {
        if (!Has("Inventario.Gestionar")) return Forbid();
        if (RequireEmpresa() is not int eid) return RedirectToSoporte();
        var r = await _inv.RegistrarSalidaAsync(eid, model, _currentUser.Username, ct);
        TempData[r.IsSuccess ? "Success" : "Error"] = r.IsSuccess ? "Salida registrada." : r.Error;
        return RedirectToAction(nameof(Kardex), new { productoId = model.ProductoId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Ajuste(AjusteStockRequest model, CancellationToken ct)
    {
        if (!Has("Inventario.Gestionar")) return Forbid();
        if (RequireEmpresa() is not int eid) return RedirectToSoporte();
        var r = await _inv.AjustarAsync(eid, model, _currentUser.Username, ct);
        TempData[r.IsSuccess ? "Success" : "Error"] = r.IsSuccess ? "Stock ajustado." : r.Error;
        return RedirectToAction(nameof(Kardex), new { productoId = model.ProductoId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> StockMinimo(SetStockMinimoRequest model, CancellationToken ct)
    {
        if (!Has("Inventario.Gestionar")) return Forbid();
        if (RequireEmpresa() is not int eid) return RedirectToSoporte();
        var r = await _inv.SetStockMinimoAsync(eid, model, _currentUser.Username, ct);
        TempData[r.IsSuccess ? "Success" : "Error"] = r.IsSuccess ? "Stock mínimo actualizado." : r.Error;
        return RedirectToAction(nameof(Kardex), new { productoId = model.ProductoId });
    }

    private bool Has(string codigo) => _currentUser.TipoUsuarioCodigo == "SUPERADMIN" || _currentUser.HasPermiso(codigo);
    private int? RequireEmpresa() => _empresaContext.CurrentEmpresaId;

    private IActionResult RedirectToSoporte()
    {
        if (_currentUser.TipoUsuarioCodigo == "SUPERADMIN")
        {
            TempData["Error"] = "El inventario opera dentro de una empresa. Selecciona una en modo soporte primero.";
            return RedirectToAction("Index", "Soporte");
        }
        return RedirectToAction("Index", "Home");
    }
}
