using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NeoSTP.Web.Auth;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Common;
using NeoSTP.Application.Empresas;
using NeoSTP.Application.Rrhh;
using NeoSTP.Application.Rrhh.Dtos;

namespace NeoSTP.Web.Controllers;

/// <summary>NEORRHH â€” gestiÃ³n de empleados + vista previa de nÃ³mina. Permisos Rrhh.Empleados.*.</summary>
[Authorize]
[RequireModulo("NEORRHH")]
public class EmpleadosController : Controller
{
    public static readonly string[] TiposDocumento = ["DUI", "NIT", "PASAPORTE", "CARNET_RESIDENTE"];
    public static readonly string[] TiposContrato = ["INDEFINIDO", "TEMPORAL", "SERVICIOS"];
    public static readonly string[] Periodicidades = ["QUINCENAL", "MENSUAL"];

    private readonly IEmpleadosService _empleados;
    private readonly ICurrentUser _currentUser;
    private readonly IEmpresaContext _empresaContext;

    public EmpleadosController(IEmpleadosService empleados, ICurrentUser currentUser, IEmpresaContext empresaContext)
    {
        _empleados = empleados;
        _currentUser = currentUser;
        _empresaContext = empresaContext;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? search, int page = 1, CancellationToken ct = default)
    {
        if (!Has("Rrhh.Empleados.Ver")) return Forbid();
        if (RequireEmpresa() is not int eid) return RedirectToSoporte();

        var result = await _empleados.GetListAsync(eid, new PagedQuery { Search = search, Page = page, PageSize = 20 }, ct);
        ViewBag.Search = search;
        ViewBag.PuedeGestionar = Has("Rrhh.Empleados.Gestionar");
        return View(result.Value);
    }

    [HttpGet]
    public async Task<IActionResult> Detalle(int id, CancellationToken ct)
    {
        if (!Has("Rrhh.Empleados.Ver")) return Forbid();
        if (RequireEmpresa() is not int eid) return RedirectToSoporte();

        var result = await _empleados.GetAsync(eid, id, ct);
        if (result.IsFailure) return NotFound();
        ViewBag.PuedeGestionar = Has("Rrhh.Empleados.Gestionar");
        ViewBag.PuedeVerNomina = Has("Rrhh.Nomina.Ver");
        return View(result.Value);
    }

    [HttpGet]
    public IActionResult Create()
    {
        if (!Has("Rrhh.Empleados.Gestionar")) return Forbid();
        if (RequireEmpresa() is not int) return RedirectToSoporte();
        Listas();
        return View(new CreateEmpleadoRequest { FechaIngreso = DateOnly.FromDateTime(DateTime.UtcNow) });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateEmpleadoRequest model, CancellationToken ct)
    {
        if (!Has("Rrhh.Empleados.Gestionar")) return Forbid();
        if (RequireEmpresa() is not int eid) return RedirectToSoporte();

        var result = await _empleados.CreateAsync(eid, model, _currentUser.Username, ct);
        if (result.IsFailure)
        {
            foreach (var e in result.ValidationErrors ?? new[] { result.Error ?? "Error." }) ModelState.AddModelError(string.Empty, e);
            Listas();
            return View(model);
        }
        TempData["Success"] = "Empleado registrado.";
        return RedirectToAction(nameof(Detalle), new { id = result.Value!.Id });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id, CancellationToken ct)
    {
        if (!Has("Rrhh.Empleados.Gestionar")) return Forbid();
        if (RequireEmpresa() is not int eid) return RedirectToSoporte();

        var result = await _empleados.GetAsync(eid, id, ct);
        if (result.IsFailure) return NotFound();
        var d = result.Value!;
        Listas();
        ViewBag.EmpleadoId = id;
        return View(new UpdateEmpleadoRequest
        {
            Codigo = d.Codigo, Nombres = d.Nombres, Apellidos = d.Apellidos, TipoDocumento = d.TipoDocumento,
            NumeroDocumento = d.NumeroDocumento, Nit = d.Nit, IsssNumero = d.IsssNumero,
            AfpInstitucion = d.AfpInstitucion, AfpNumero = d.AfpNumero, FechaNacimiento = d.FechaNacimiento,
            FechaIngreso = d.FechaIngreso, Cargo = d.Cargo, Email = d.Email, Telefono = d.Telefono,
            TipoContrato = d.TipoContrato, SalarioMensual = d.SalarioMensual, PeriodicidadPago = d.PeriodicidadPago,
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, UpdateEmpleadoRequest model, CancellationToken ct)
    {
        if (!Has("Rrhh.Empleados.Gestionar")) return Forbid();
        if (RequireEmpresa() is not int eid) return RedirectToSoporte();

        var result = await _empleados.UpdateAsync(eid, id, model, _currentUser.Username, ct);
        if (result.IsFailure)
        {
            foreach (var e in result.ValidationErrors ?? new[] { result.Error ?? "Error." }) ModelState.AddModelError(string.Empty, e);
            Listas();
            ViewBag.EmpleadoId = id;
            return View(model);
        }
        TempData["Success"] = "Empleado actualizado.";
        return RedirectToAction(nameof(Detalle), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Inactivar(int id, CancellationToken ct)
    {
        if (!Has("Rrhh.Empleados.Gestionar")) return Forbid();
        if (RequireEmpresa() is not int eid) return RedirectToSoporte();

        var result = await _empleados.InactivarAsync(eid, id, _currentUser.Username, ct);
        TempData[result.IsSuccess ? "Success" : "Error"] = result.IsSuccess ? "Empleado inactivado." : result.Error;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Restaurar(int id, CancellationToken ct)
    {
        if (!Has("Rrhh.Empleados.Gestionar")) return Forbid();
        if (RequireEmpresa() is not int eid) return RedirectToSoporte();

        var result = await _empleados.RestaurarAsync(eid, id, _currentUser.Username, ct);
        TempData[result.IsSuccess ? "Success" : "Error"] = result.IsSuccess ? "Empleado restaurado." : result.Error;
        return RedirectToAction(nameof(Index));
    }

    private void Listas()
    {
        ViewBag.TiposDocumento = TiposDocumento;
        ViewBag.TiposContrato = TiposContrato;
        ViewBag.Periodicidades = Periodicidades;
    }

    private bool Has(string codigo) => _currentUser.TipoUsuarioCodigo == "SUPERADMIN" || _currentUser.HasPermiso(codigo);
    private int? RequireEmpresa() => _empresaContext.CurrentEmpresaId;

    private IActionResult RedirectToSoporte()
    {
        if (_currentUser.TipoUsuarioCodigo == "SUPERADMIN")
        {
            TempData["Error"] = "NeoRRHH opera dentro de una empresa. Selecciona una en modo soporte primero.";
            return RedirectToAction("Index", "Soporte");
        }
        return RedirectToAction("Index", "Home");
    }
}
