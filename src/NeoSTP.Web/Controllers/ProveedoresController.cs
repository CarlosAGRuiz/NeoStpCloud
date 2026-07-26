using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NeoSTP.Web.Auth;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Common;
using NeoSTP.Application.Compras;
using NeoSTP.Application.Compras.Dtos;
using NeoSTP.Application.Empresas;

namespace NeoSTP.Web.Controllers;

/// <summary>NEOCOMPRAS â€” maestro de proveedores. Permisos Compras.Proveedores.*.</summary>
[Authorize]
[RequireModulo("COMPRAS")]
public class ProveedoresController : Controller
{
    private readonly ICompraService _compras;
    private readonly ICurrentUser _currentUser;
    private readonly IEmpresaContext _empresaContext;

    public ProveedoresController(ICompraService compras, ICurrentUser currentUser, IEmpresaContext empresaContext)
    {
        _compras = compras;
        _currentUser = currentUser;
        _empresaContext = empresaContext;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? search, int page = 1, CancellationToken ct = default)
    {
        if (!Has("Compras.Proveedores.Ver")) return Forbid();
        if (RequireEmpresa() is not int eid) return RedirectToSoporte();

        var result = await _compras.ListProveedoresAsync(eid, new PagedQuery { Search = search, Page = page, PageSize = 20 }, ct);
        ViewBag.Search = search;
        ViewBag.PuedeGestionar = Has("Compras.Proveedores.Gestionar");
        return View(result.Value);
    }

    [HttpGet]
    public async Task<IActionResult> Detalle(int id, CancellationToken ct)
    {
        if (!Has("Compras.Proveedores.Ver")) return Forbid();
        if (RequireEmpresa() is not int eid) return RedirectToSoporte();

        var result = await _compras.GetProveedorAsync(eid, id, ct);
        if (result.IsFailure) return NotFound();
        ViewBag.PuedeGestionar = Has("Compras.Proveedores.Gestionar");
        ViewBag.PuedeVerCompras = Has("Compras.Ver");
        return View(result.Value);
    }

    [HttpGet]
    public IActionResult Create()
    {
        if (!Has("Compras.Proveedores.Gestionar")) return Forbid();
        return View(new CreateProveedorRequest());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateProveedorRequest model, CancellationToken ct)
    {
        if (!Has("Compras.Proveedores.Gestionar")) return Forbid();
        if (RequireEmpresa() is not int eid) return RedirectToSoporte();

        var result = await _compras.CrearProveedorAsync(eid, model, _currentUser.Username, ct);
        if (result.IsFailure)
        {
            foreach (var e in result.ValidationErrors ?? new[] { result.Error ?? "Error." }) ModelState.AddModelError(string.Empty, e);
            return View(model);
        }
        TempData["Success"] = "Proveedor creado.";
        return RedirectToAction(nameof(Detalle), new { id = result.Value!.Id });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id, CancellationToken ct)
    {
        if (!Has("Compras.Proveedores.Gestionar")) return Forbid();
        if (RequireEmpresa() is not int eid) return RedirectToSoporte();

        var result = await _compras.GetProveedorAsync(eid, id, ct);
        if (result.IsFailure) return NotFound();
        var p = result.Value!;
        ViewBag.ProveedorId = id;
        ViewBag.Codigo = p.Codigo;
        return View(new UpdateProveedorRequest
        {
            Nombre = p.Nombre, Nit = p.Nit, Nrc = p.Nrc, Contacto = p.Contacto,
            Telefono = p.Telefono, Email = p.Email, Direccion = p.Direccion, PlazoDiasDefault = p.PlazoDiasDefault,
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, UpdateProveedorRequest model, CancellationToken ct)
    {
        if (!Has("Compras.Proveedores.Gestionar")) return Forbid();
        if (RequireEmpresa() is not int eid) return RedirectToSoporte();

        var result = await _compras.ActualizarProveedorAsync(eid, id, model, _currentUser.Username, ct);
        if (result.IsFailure)
        {
            foreach (var e in result.ValidationErrors ?? new[] { result.Error ?? "Error." }) ModelState.AddModelError(string.Empty, e);
            ViewBag.ProveedorId = id;
            return View(model);
        }
        TempData["Success"] = "Proveedor actualizado.";
        return RedirectToAction(nameof(Detalle), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Inactivar(int id, CancellationToken ct)
    {
        if (!Has("Compras.Proveedores.Gestionar")) return Forbid();
        if (RequireEmpresa() is not int eid) return RedirectToSoporte();
        var result = await _compras.InactivarProveedorAsync(eid, id, _currentUser.Username, ct);
        TempData[result.IsSuccess ? "Success" : "Error"] = result.IsSuccess ? "Proveedor inactivado." : result.Error;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reactivar(int id, CancellationToken ct)
    {
        if (!Has("Compras.Proveedores.Gestionar")) return Forbid();
        if (RequireEmpresa() is not int eid) return RedirectToSoporte();
        var result = await _compras.ReactivarProveedorAsync(eid, id, _currentUser.Username, ct);
        TempData[result.IsSuccess ? "Success" : "Error"] = result.IsSuccess ? "Proveedor reactivado." : result.Error;
        return RedirectToAction(nameof(Index));
    }

    private bool Has(string codigo) => _currentUser.TipoUsuarioCodigo == "SUPERADMIN" || _currentUser.HasPermiso(codigo);
    private int? RequireEmpresa() => _empresaContext.CurrentEmpresaId;

    private IActionResult RedirectToSoporte()
    {
        if (_currentUser.TipoUsuarioCodigo == "SUPERADMIN")
        {
            TempData["Error"] = "Compras opera dentro de una empresa. Selecciona una en modo soporte primero.";
            return RedirectToAction("Index", "Soporte");
        }
        return RedirectToAction("Index", "Home");
    }
}
