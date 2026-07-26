using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NeoSTP.Web.Auth;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Common;
using NeoSTP.Application.Empresas;
using NeoSTP.Application.Profit;
using NeoSTP.Application.Profit.Dtos;

namespace NeoSTP.Web.Controllers;

/// <summary>NeoProfit â€” gestiÃ³n de compras / insumos (grid + CRUD con soft-delete).</summary>
[Authorize]
[RequireModulo("NEOPROFIT")]
public class ProfitComprasController : Controller
{
    private readonly IProfitService _profit;
    private readonly ICurrentUser _currentUser;
    private readonly IEmpresaContext _empresaContext;

    public ProfitComprasController(IProfitService profit, ICurrentUser currentUser, IEmpresaContext empresaContext)
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
        var result = await _profit.ListComprasAsync(eid, new PagedQuery { Search = search, Page = page, PageSize = 20 }, periodo, ct);
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
        return View(new CreateProfitCompraRequest { Fecha = DateOnly.FromDateTime(DateTime.UtcNow) });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateProfitCompraRequest model, CancellationToken ct)
    {
        if (!Has("Profit.Gestionar")) return Forbid();
        if (RequireEmpresa() is not int eid) return Forbid();
        if (!ModelState.IsValid) return View(model);

        var result = await _profit.CreateCompraAsync(eid, model, _currentUser.Username, ct);
        if (result.IsFailure)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Error.");
            return View(model);
        }
        TempData["Success"] = "Compra registrada.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id, CancellationToken ct)
    {
        if (!Has("Profit.Gestionar")) return Forbid();
        if (RequireEmpresa() is not int eid) return Forbid();

        var result = await _profit.GetCompraAsync(eid, id, ct);
        if (result.IsFailure) return NotFound();
        return View(result.Value);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, ProfitCompraDto model, CancellationToken ct)
    {
        if (!Has("Profit.Gestionar")) return Forbid();
        if (RequireEmpresa() is not int eid) return Forbid();
        if (!ModelState.IsValid) return View(model);

        var update = new UpdateProfitCompraRequest
        {
            Fecha = model.Fecha, Proveedor = model.Proveedor, NumeroDocumento = model.NumeroDocumento,
            Descripcion = model.Descripcion, Subtotal = model.Subtotal, IvaMonto = model.IvaMonto,
        };
        var result = await _profit.UpdateCompraAsync(eid, id, update, _currentUser.Username, ct);
        if (result.IsFailure)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Error.");
            return View(model);
        }
        TempData["Success"] = "Compra actualizada.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Inactivar(int id, CancellationToken ct)
    {
        if (!Has("Profit.Gestionar")) return Forbid();
        if (RequireEmpresa() is not int eid) return Forbid();

        var result = await _profit.InactivarCompraAsync(eid, id, _currentUser.Username, ct);
        TempData[result.IsSuccess ? "Success" : "Error"] = result.IsSuccess ? "Compra inactivada." : result.Error;
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
