using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Common;
using NeoSTP.Application.Empresas;
using NeoSTP.Application.Rrhh;
using NeoSTP.Application.Rrhh.Dtos;
using NeoSTP.Shared;

namespace NeoSTP.Web.Controllers;

/// <summary>NEORRHH — corridas de planilla. Permisos Rrhh.Nomina.Ver / Rrhh.Nomina.Gestionar.</summary>
[Authorize]
public class PlanillaController : Controller
{
    private readonly IPlanillaService _planilla;
    private readonly ICurrentUser _currentUser;
    private readonly IEmpresaContext _empresaContext;

    public PlanillaController(IPlanillaService planilla, ICurrentUser currentUser, IEmpresaContext empresaContext)
    {
        _planilla = planilla;
        _currentUser = currentUser;
        _empresaContext = empresaContext;
    }

    [HttpGet]
    public async Task<IActionResult> Index(int page = 1, CancellationToken ct = default)
    {
        if (!Has("Rrhh.Nomina.Ver")) return Forbid();
        if (RequireEmpresa() is not int eid) return RedirectToSoporte();

        var result = await _planilla.ListAsync(eid, new PagedQuery { Page = page, PageSize = 20 }, ct);
        ViewBag.PuedeGestionar = Has("Rrhh.Nomina.Gestionar");
        return View(result.Value);
    }

    [HttpGet]
    public async Task<IActionResult> Detalle(int id, CancellationToken ct)
    {
        if (!Has("Rrhh.Nomina.Ver")) return Forbid();
        if (RequireEmpresa() is not int eid) return RedirectToSoporte();

        var result = await _planilla.GetAsync(eid, id, ct);
        if (result.IsFailure) return NotFound();
        ViewBag.PuedeGestionar = Has("Rrhh.Nomina.Gestionar");
        return View(result.Value);
    }

    [HttpGet]
    public IActionResult Crear()
    {
        if (!Has("Rrhh.Nomina.Gestionar")) return Forbid();
        var hoy = DateTime.UtcNow;
        return View(new CrearPlanillaRequest { Anio = hoy.Year, Mes = hoy.Month, Quincena = hoy.Day <= 15 ? 1 : 2 });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Crear(CrearPlanillaRequest model, CancellationToken ct)
    {
        if (!Has("Rrhh.Nomina.Gestionar")) return Forbid();
        if (RequireEmpresa() is not int eid) return RedirectToSoporte();

        var result = await _planilla.CrearAsync(eid, model, _currentUser.Username, ct);
        if (result.IsFailure)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Error.");
            return View(model);
        }
        TempData["Success"] = $"Planilla {result.Value!.Etiqueta} calculada ({result.Value.Detalles.Count} empleados).";
        return RedirectToAction(nameof(Detalle), new { id = result.Value.Id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Recalcular(int id, CancellationToken ct)
    {
        if (!Has("Rrhh.Nomina.Gestionar")) return Forbid();
        if (RequireEmpresa() is not int eid) return RedirectToSoporte();
        var result = await _planilla.RecalcularAsync(eid, id, _currentUser.Username, ct);
        TempData[result.IsSuccess ? "Success" : "Error"] = result.IsSuccess ? "Planilla recalculada." : result.Error;
        return RedirectToAction(nameof(Detalle), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cerrar(int id, CancellationToken ct)
    {
        if (!Has("Rrhh.Nomina.Gestionar")) return Forbid();
        if (RequireEmpresa() is not int eid) return RedirectToSoporte();
        var result = await _planilla.CerrarAsync(eid, id, _currentUser.Username, ct);
        TempData[result.IsSuccess ? "Success" : "Error"] = result.IsSuccess ? "Planilla cerrada y registrada como gasto." : result.Error;
        return RedirectToAction(nameof(Detalle), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Anular(int id, CancellationToken ct)
    {
        if (!Has("Rrhh.Nomina.Gestionar")) return Forbid();
        if (RequireEmpresa() is not int eid) return RedirectToSoporte();
        var result = await _planilla.AnularAsync(eid, id, _currentUser.Username, ct);
        TempData[result.IsSuccess ? "Success" : "Error"] = result.IsSuccess ? "Planilla anulada." : result.Error;
        return RedirectToAction(nameof(Detalle), new { id });
    }

    [HttpGet]
    public async Task<IActionResult> ExportCsv(int id, CancellationToken ct)
    {
        if (!Has("Rrhh.Nomina.Ver")) return Forbid();
        if (RequireEmpresa() is not int eid) return RedirectToSoporte();

        var result = await _planilla.GetAsync(eid, id, ct);
        if (result.IsFailure) return NotFound();
        var p = result.Value!;

        var csv = new CsvExporter("Codigo", "Empleado", "SalarioMensual", "Devengado", "ISSS", "AFP", "Renta",
            "OtrosDescuentos", "TotalDeducciones", "Neto", "ISSS_Patronal", "AFP_Patronal", "CostoPatronal");
        foreach (var d in p.Detalles)
        {
            csv.AddRow(d.EmpleadoCodigo, d.EmpleadoNombre, d.SalarioMensual, d.Devengado, d.Isss, d.Afp, d.Renta,
                d.OtrosDescuentos, d.TotalDeducciones, d.SalarioNeto, d.IsssPatronal, d.AfpPatronal, d.CostoPatronal);
        }
        return File(csv.ToBytes(), "text/csv", $"planilla_{p.Anio}_{p.Mes:00}_Q{p.Quincena}.csv");
    }

    private bool Has(string codigo) => _currentUser.TipoUsuarioCodigo == "SUPERADMIN" || _currentUser.HasPermiso(codigo);
    private int? RequireEmpresa() => _empresaContext.CurrentEmpresaId;

    private IActionResult RedirectToSoporte()
    {
        if (_currentUser.TipoUsuarioCodigo == "SUPERADMIN")
        {
            TempData["Error"] = "La planilla opera dentro de una empresa. Selecciona una en modo soporte primero.";
            return RedirectToAction("Index", "Soporte");
        }
        return RedirectToAction("Index", "Home");
    }
}
