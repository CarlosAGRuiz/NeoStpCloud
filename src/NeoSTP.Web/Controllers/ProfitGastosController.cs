using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Common;
using NeoSTP.Application.Empresas;
using NeoSTP.Application.Profit;
using NeoSTP.Application.Profit.Dtos;

namespace NeoSTP.Web.Controllers;

/// <summary>NeoProfit — gestión de gastos operativos (grid + CRUD con soft-delete).</summary>
[Authorize]
public class ProfitGastosController : Controller
{
    public static readonly string[] Categorias =
        ["ALQUILER", "SERVICIOS", "PLANILLA", "MARKETING", "TRANSPORTE", "IMPUESTOS", "MANTENIMIENTO", "OTROS"];

    private readonly IProfitService _profit;
    private readonly ICurrentUser _currentUser;
    private readonly IEmpresaContext _empresaContext;

    public ProfitGastosController(IProfitService profit, ICurrentUser currentUser, IEmpresaContext empresaContext)
    {
        _profit = profit;
        _currentUser = currentUser;
        _empresaContext = empresaContext;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? search, DateOnly? desde, DateOnly? hasta, int page = 1, CancellationToken ct = default)
    {
        if (!Has("Profit.Ver")) return Forbid();
        if (RequireEmpresa() is not int eid) return RedirectToSoporte();

        var periodo = desde is null && hasta is null ? null : new ProfitPeriodoQuery { Desde = desde, Hasta = hasta };
        var result = await _profit.ListGastosAsync(eid, new PagedQuery { Search = search, Page = page, PageSize = 20 }, periodo, ct);
        ViewBag.Search = search;
        ViewBag.Desde = desde;
        ViewBag.Hasta = hasta;
        ViewBag.PuedeGestionar = Has("Profit.Gestionar");
        return View(result.Value);
    }

    [HttpGet]
    public IActionResult Create()
    {
        if (!Has("Profit.Gestionar")) return Forbid();
        ViewBag.Categorias = Categorias;
        return View(new CreateProfitGastoRequest { Fecha = DateOnly.FromDateTime(DateTime.UtcNow) });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateProfitGastoRequest model, CancellationToken ct)
    {
        if (!Has("Profit.Gestionar")) return Forbid();
        if (RequireEmpresa() is not int eid) return Forbid();
        if (!ModelState.IsValid) { ViewBag.Categorias = Categorias; return View(model); }

        var result = await _profit.CreateGastoAsync(eid, model, _currentUser.Username, ct);
        if (result.IsFailure)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Error.");
            ViewBag.Categorias = Categorias;
            return View(model);
        }
        TempData["Success"] = "Gasto registrado.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id, CancellationToken ct)
    {
        if (!Has("Profit.Gestionar")) return Forbid();
        if (RequireEmpresa() is not int eid) return Forbid();

        var result = await _profit.GetGastoAsync(eid, id, ct);
        if (result.IsFailure) return NotFound();
        ViewBag.Categorias = Categorias;
        return View(result.Value);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, ProfitGastoDto model, CancellationToken ct)
    {
        if (!Has("Profit.Gestionar")) return Forbid();
        if (RequireEmpresa() is not int eid) return Forbid();
        if (!ModelState.IsValid) { ViewBag.Categorias = Categorias; return View(model); }

        var update = new UpdateProfitGastoRequest
        {
            Fecha = model.Fecha, Categoria = model.Categoria, Descripcion = model.Descripcion,
            Proveedor = model.Proveedor, Monto = model.Monto, IvaMonto = model.IvaMonto, IvaDeducible = model.IvaDeducible,
            ProveedorNoDomiciliado = model.ProveedorNoDomiciliado,
            RetencionRentaMonto = model.RetencionRentaMonto,
            IvaImportacionMonto = model.IvaImportacionMonto,
        };
        var result = await _profit.UpdateGastoAsync(eid, id, update, _currentUser.Username, ct);
        if (result.IsFailure)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Error.");
            ViewBag.Categorias = Categorias;
            return View(model);
        }
        TempData["Success"] = "Gasto actualizado.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Inactivar(int id, CancellationToken ct)
    {
        if (!Has("Profit.Gestionar")) return Forbid();
        if (RequireEmpresa() is not int eid) return Forbid();

        var result = await _profit.InactivarGastoAsync(eid, id, _currentUser.Username, ct);
        TempData[result.IsSuccess ? "Success" : "Error"] = result.IsSuccess ? "Gasto inactivado." : result.Error;
        return RedirectToAction(nameof(Index));
    }

    private bool Has(string codigo) => _currentUser.TipoUsuarioCodigo == "SUPERADMIN" || _currentUser.HasPermiso(codigo);
    private int? RequireEmpresa() => _empresaContext.CurrentEmpresaId;

    private IActionResult RedirectToSoporte()
    {
        if (_currentUser.TipoUsuarioCodigo == "SUPERADMIN")
        {
            TempData["Error"] = "NeoProfit opera dentro de una empresa. Selecciona una en modo soporte primero.";
            return RedirectToAction("Index", "Soporte");
        }
        return RedirectToAction("Index", "Home");
    }
}
