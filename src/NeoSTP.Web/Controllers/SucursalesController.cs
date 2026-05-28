using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Common;
using NeoSTP.Application.Empresas;
using NeoSTP.Application.Empresas.Dtos;

namespace NeoSTP.Web.Controllers;

[Authorize]
public class SucursalesController : Controller
{
    private readonly ISucursalesService _sucursales;
    private readonly IPuntosVentaService _puntosVenta;
    private readonly ICurrentUser _currentUser;
    private readonly IEmpresaContext _empresaContext;

    public SucursalesController(
        ISucursalesService sucursales,
        IPuntosVentaService puntosVenta,
        ICurrentUser currentUser,
        IEmpresaContext empresaContext)
    {
        _sucursales   = sucursales;
        _puntosVenta  = puntosVenta;
        _currentUser  = currentUser;
        _empresaContext = empresaContext;
    }

    // ── Sucursales ────────────────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] string? search, [FromQuery] int page = 1, CancellationToken ct = default)
    {
        if (!Has("Empresas.Ver")) return Forbid();
        if (RequireEmpresa() is not int eid) return RedirectToSoporte();

        var result = await _sucursales.GetListAsync(eid, new PagedQuery { Search = search, Page = page, PageSize = 20 }, ct);
        ViewBag.Search = search;
        return View(result.Value);
    }

    [HttpGet]
    public IActionResult Create()
    {
        if (!Has("Empresas.Administrar")) return Forbid();
        if (RequireEmpresa() is null) return RedirectToSoporte();
        return View(new CreateSucursalRequest());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateSucursalRequest model, CancellationToken ct)
    {
        if (!Has("Empresas.Administrar")) return Forbid();
        if (RequireEmpresa() is not int eid) return Forbid();

        if (!ModelState.IsValid) return View(model);

        var result = await _sucursales.CreateAsync(eid, model, _currentUser.Username, ct);
        if (result.IsFailure)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Error.");
            foreach (var e in result.ValidationErrors) ModelState.AddModelError(string.Empty, e);
            return View(model);
        }

        TempData["Success"] = $"Sucursal {result.Value!.Nombre} creada.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id, CancellationToken ct)
    {
        if (!Has("Empresas.Administrar")) return Forbid();
        if (RequireEmpresa() is not int eid) return Forbid();

        var result = await _sucursales.GetByIdAsync(eid, id, ct);
        if (result.IsFailure) return NotFound();

        var s = result.Value!;
        return View(new UpdateSucursalRequest
        {
            Nombre                   = s.Nombre,
            CodigoEstablecimientoMh  = s.CodigoEstablecimientoMh,
            TipoEstablecimientoCodigo= s.TipoEstablecimientoCodigo,
            Direccion                = s.Direccion,
            Telefono                 = s.Telefono,
            Departamento             = s.Departamento,
            Municipio                = s.Municipio,
            EstadoCodigo             = s.EstadoCodigo,
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, UpdateSucursalRequest model, CancellationToken ct)
    {
        if (!Has("Empresas.Administrar")) return Forbid();
        if (RequireEmpresa() is not int eid) return Forbid();

        if (!ModelState.IsValid) return View(model);

        var result = await _sucursales.UpdateAsync(eid, id, model, _currentUser.Username, ct);
        if (result.IsFailure)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Error.");
            return View(model);
        }

        TempData["Success"] = "Sucursal actualizada.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Inactivar(int id, CancellationToken ct)
    {
        if (!Has("Empresas.Administrar")) return Forbid();
        if (RequireEmpresa() is not int eid) return Forbid();

        var result = await _sucursales.InactivarAsync(eid, id, _currentUser.Username, ct);
        TempData[result.IsSuccess ? "Success" : "Error"] = result.IsSuccess ? "Sucursal inactivada." : result.Error;
        return RedirectToAction(nameof(Index));
    }

    // ── Puntos de Venta ───────────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> PuntosVenta([FromQuery] int? sucursalId, [FromQuery] string? search, [FromQuery] int page = 1, CancellationToken ct = default)
    {
        if (!Has("Empresas.Ver")) return Forbid();
        if (RequireEmpresa() is not int eid) return RedirectToSoporte();

        var result = await _puntosVenta.GetListAsync(eid, sucursalId, new PagedQuery { Search = search, Page = page, PageSize = 20 }, ct);
        var sucursalesList = await _sucursales.GetListAsync(eid, new PagedQuery { PageSize = 100 }, ct);

        ViewBag.Sucursales   = sucursalesList.Value?.Items ?? [];
        ViewBag.SucursalId   = sucursalId;
        ViewBag.Search       = search;
        return View(result.Value);
    }

    [HttpGet]
    public async Task<IActionResult> CreatePuntoVenta(int? sucursalId, CancellationToken ct)
    {
        if (!Has("Empresas.Administrar")) return Forbid();
        if (RequireEmpresa() is not int eid) return RedirectToSoporte();

        var sucursalesList = await _sucursales.GetListAsync(eid, new PagedQuery { PageSize = 100 }, ct);
        ViewBag.Sucursales = sucursalesList.Value?.Items ?? [];
        return View(new CreatePuntoVentaRequest { SucursalId = sucursalId ?? 0 });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreatePuntoVenta(CreatePuntoVentaRequest model, CancellationToken ct)
    {
        if (!Has("Empresas.Administrar")) return Forbid();
        if (RequireEmpresa() is not int eid) return Forbid();

        if (!ModelState.IsValid)
        {
            var sucursalesList = await _sucursales.GetListAsync(eid, new PagedQuery { PageSize = 100 }, ct);
            ViewBag.Sucursales = sucursalesList.Value?.Items ?? [];
            return View(model);
        }

        var result = await _puntosVenta.CreateAsync(eid, model, _currentUser.Username, ct);
        if (result.IsFailure)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Error.");
            foreach (var e in result.ValidationErrors) ModelState.AddModelError(string.Empty, e);
            var sucursalesList = await _sucursales.GetListAsync(eid, new PagedQuery { PageSize = 100 }, ct);
            ViewBag.Sucursales = sucursalesList.Value?.Items ?? [];
            return View(model);
        }

        TempData["Success"] = $"Punto de venta {result.Value!.Nombre} creado.";
        return RedirectToAction(nameof(PuntosVenta));
    }

    [HttpGet]
    public async Task<IActionResult> EditPuntoVenta(int id, CancellationToken ct)
    {
        if (!Has("Empresas.Administrar")) return Forbid();
        if (RequireEmpresa() is not int eid) return Forbid();

        var result = await _puntosVenta.GetByIdAsync(eid, id, ct);
        if (result.IsFailure) return NotFound();

        var pv = result.Value!;
        ViewBag.SucursalNombre = pv.SucursalNombre;
        return View(new UpdatePuntoVentaRequest
        {
            Nombre               = pv.Nombre,
            CodigoPuntoVentaMh   = pv.CodigoPuntoVentaMh,
            EstadoCodigo         = pv.EstadoCodigo,
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditPuntoVenta(int id, UpdatePuntoVentaRequest model, CancellationToken ct)
    {
        if (!Has("Empresas.Administrar")) return Forbid();
        if (RequireEmpresa() is not int eid) return Forbid();

        if (!ModelState.IsValid) return View(model);

        var result = await _puntosVenta.UpdateAsync(eid, id, model, _currentUser.Username, ct);
        if (result.IsFailure)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Error.");
            return View(model);
        }

        TempData["Success"] = "Punto de venta actualizado.";
        return RedirectToAction(nameof(PuntosVenta));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> InactivarPuntoVenta(int id, CancellationToken ct)
    {
        if (!Has("Empresas.Administrar")) return Forbid();
        if (RequireEmpresa() is not int eid) return Forbid();

        var result = await _puntosVenta.InactivarAsync(eid, id, _currentUser.Username, ct);
        TempData[result.IsSuccess ? "Success" : "Error"] = result.IsSuccess ? "Punto de venta inactivado." : result.Error;
        return RedirectToAction(nameof(PuntosVenta));
    }

    // ── helpers ───────────────────────────────────────────────────────────

    private bool Has(string codigo)
        => _currentUser.TipoUsuarioCodigo == "SUPERADMIN" || _currentUser.HasPermiso(codigo);

    private int? RequireEmpresa() => _empresaContext.CurrentEmpresaId;

    private IActionResult RedirectToSoporte()
    {
        if (_currentUser.TipoUsuarioCodigo == "SUPERADMIN")
        {
            TempData["Error"] = "Esta pantalla opera dentro de una empresa. Selecciona una en modo soporte primero.";
            return RedirectToAction("Index", "Soporte");
        }
        return RedirectToAction("Index", "Home");
    }
}
