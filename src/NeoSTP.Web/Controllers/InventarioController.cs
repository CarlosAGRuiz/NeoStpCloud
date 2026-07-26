using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NeoSTP.Web.Auth;
using Microsoft.EntityFrameworkCore;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Common;
using NeoSTP.Application.Empresas;
using NeoSTP.Application.Inventario;
using NeoSTP.Application.Inventario.Dtos;

namespace NeoSTP.Web.Controllers;

/// <summary>INVENTARIO â€” existencias y kardex. Permisos Inventario.Ver / Inventario.Gestionar.</summary>
[Authorize]
[RequireModulo("INVENTARIO")]
public class InventarioController : Controller
{
    private readonly IInventarioService _inv;
    private readonly ICurrentUser _currentUser;
    private readonly IEmpresaContext _empresaContext;
    private readonly NeoSTP.Infrastructure.Persistence.NeoStpDbContext _db;

    public InventarioController(IInventarioService inv, ICurrentUser currentUser, IEmpresaContext empresaContext,
        NeoSTP.Infrastructure.Persistence.NeoStpDbContext db)
    {
        _inv = inv;
        _currentUser = currentUser;
        _empresaContext = empresaContext;
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? search, bool soloStockBajo = false, int? sucursalId = null, int page = 1, CancellationToken ct = default)
    {
        if (!Has("Inventario.Ver")) return Forbid();
        if (RequireEmpresa() is not int eid) return RedirectToSoporte();

        var result = await _inv.ListExistenciasAsync(eid, soloStockBajo, new PagedQuery { Search = search, Page = page, PageSize = 20 }, sucursalId, ct);
        var resumen = await _inv.ResumenAsync(eid, ct);
        ViewBag.Search = search;
        ViewBag.SoloStockBajo = soloStockBajo;
        ViewBag.SucursalId = sucursalId;
        ViewBag.Sucursales = await CargarSucursalesAsync(eid, ct);
        ViewBag.Resumen = resumen.Value;
        ViewBag.PuedeGestionar = Has("Inventario.Gestionar");
        return View(result.Value);
    }

    [HttpGet]
    public async Task<IActionResult> Kardex(int productoId, int? sucursalId = null, int page = 1, CancellationToken ct = default)
    {
        if (!Has("Inventario.Ver")) return Forbid();
        if (RequireEmpresa() is not int eid) return RedirectToSoporte();

        var existencia = await _inv.GetExistenciaAsync(eid, productoId, sucursalId, ct);
        if (existencia.IsFailure) return NotFound();
        var kardex = await _inv.GetKardexAsync(eid, productoId, new PagedQuery { Page = page, PageSize = 30 }, sucursalId, ct);
        ViewBag.Existencia = existencia.Value;
        ViewBag.SucursalId = sucursalId;
        ViewBag.Sucursales = await CargarSucursalesAsync(eid, ct);
        ViewBag.PuedeGestionar = Has("Inventario.Gestionar");
        return View(kardex.Value);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Traslado(TrasladoInventarioRequest model, CancellationToken ct)
    {
        if (!Has("Inventario.Gestionar")) return Forbid();
        if (RequireEmpresa() is not int eid) return RedirectToSoporte();

        var r = await _inv.TrasladarAsync(eid, model, _currentUser.Username, ct);
        TempData[r.IsSuccess ? "Success" : "Error"] = r.IsSuccess ? "Traslado aplicado." : r.Error;
        return RedirectToAction(nameof(Kardex), new { productoId = model.ProductoId });
    }

    private async Task<IReadOnlyList<(int Id, string Nombre)>> CargarSucursalesAsync(int eid, CancellationToken ct)
        => (await _db.Sucursales.AsNoTracking()
                .Where(s => s.EmpresaId == eid && s.EstadoCodigo == "ACTIVO")
                .OrderBy(s => s.Nombre)
                .Select(s => new { s.Id, s.Nombre })
                .ToListAsync(ct))
            .Select(s => (s.Id, s.Nombre)).ToList();

    [HttpGet]
    public async Task<IActionResult> Lotes(int? productoId, bool soloPorVencer = false, CancellationToken ct = default)
    {
        if (!Has("Inventario.Ver")) return Forbid();
        if (RequireEmpresa() is not int eid) return RedirectToSoporte();

        var result = await _inv.ListLotesAsync(eid, productoId, soloPorVencer, diasUmbral: 30, ct);
        ViewBag.ProductoId = productoId;
        ViewBag.SoloPorVencer = soloPorVencer;
        return View(result.Value);
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
        TempData[r.IsSuccess ? "Success" : "Error"] = r.IsSuccess ? "Stock mÃ­nimo actualizado." : r.Error;
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
