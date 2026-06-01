using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Dte.Diagnostico;
using NeoSTP.Application.Dte.Diagnostico.Dtos;
using NeoSTP.Application.Empresas;

namespace NeoSTP.Web.Controllers;

[Authorize]
public class DiagnosticoHaciendaController : Controller
{
    private readonly IDiagnosticoHaciendaService _service;
    private readonly ICurrentUser _currentUser;
    private readonly IEmpresaContext _empresaContext;

    public DiagnosticoHaciendaController(
        IDiagnosticoHaciendaService service,
        ICurrentUser currentUser,
        IEmpresaContext empresaContext)
    {
        _service = service;
        _currentUser = currentUser;
        _empresaContext = empresaContext;
    }

    // ──────────────────────────────────────────────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> Index(
        string? codigoError, string? fuente, bool? soloNoResueltas,
        int page = 1, CancellationToken ct = default)
    {
        if (!Has("DTE.Diagnostico")) return Forbid();
        if (RequireEmpresa() is not int eid) return RedirectToSoporte();

        var resumen = await _service.ObtenerResumenAsync(eid, ct);
        var filtro = new DiagnosticoFiltroRequest(codigoError, fuente, null, null, soloNoResueltas, page, 50);
        var ocurrencias = await _service.ListarOcurrenciasAsync(eid, filtro, ct);
        var catalogo = await _service.ListarCatalogoAsync(ct);

        ViewBag.Resumen = resumen;
        ViewBag.Catalogo = catalogo;
        ViewBag.FiltroActual = filtro;
        return View(ocurrencias);
    }

    // ──────────────────────────────────────────────────────────────────────────
    [HttpGet("DiagnosticoHacienda/Documento/{id:int}")]
    public async Task<IActionResult> Documento(int id, CancellationToken ct)
    {
        if (!Has("DTE.Diagnostico")) return Forbid();
        if (RequireEmpresa() is not int eid) return RedirectToSoporte();

        var dto = await _service.ObtenerDiagnosticoDocumentoAsync(eid, id, ct);
        if (dto is null) return NotFound();
        return View(dto);
    }

    // ──────────────────────────────────────────────────────────────────────────
    [HttpGet("DiagnosticoHacienda/Evento/{id:int}")]
    public async Task<IActionResult> Evento(int id, CancellationToken ct)
    {
        if (!Has("DTE.Diagnostico")) return Forbid();
        if (RequireEmpresa() is not int eid) return RedirectToSoporte();

        var dto = await _service.ObtenerDiagnosticoEventoAsync(eid, id, ct);
        if (dto is null) return NotFound();
        return View(dto);
    }

    // ──────────────────────────────────────────────────────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarcarResuelta(int id, CancellationToken ct)
    {
        if (!Has("DTE.Diagnostico")) return Forbid();
        if (RequireEmpresa() is not int eid) return RedirectToSoporte();

        var result = await _service.MarcarResueltaAsync(eid, id, _currentUser.Username ?? "web", ct);
        if (result.IsFailure) TempData["Error"] = result.Error;
        else TempData["Success"] = "Ocurrencia marcada como resuelta.";
        return RedirectToAction(nameof(Index));
    }

    // ──────────────────────────────────────────────────────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Sincronizar(CancellationToken ct)
    {
        if (!Has("DTE.Diagnostico")) return Forbid();
        if (RequireEmpresa() is not int eid) return RedirectToSoporte();

        var importadas = await _service.SincronizarOcurrenciasAsync(eid, ct);
        TempData["Success"] = $"Sincronización completada: {importadas} ocurrencias nuevas importadas.";
        return RedirectToAction(nameof(Index));
    }

    // ──────────────────────────────────────────────────────────────────────────
    private bool Has(string codigo)
        => _currentUser.TipoUsuarioCodigo == "SUPERADMIN" || _currentUser.HasPermiso(codigo);

    private int? RequireEmpresa() => _empresaContext.CurrentEmpresaId;

    private IActionResult RedirectToSoporte()
    {
        if (_currentUser.TipoUsuarioCodigo == "SUPERADMIN")
        {
            TempData["Error"] = "Selecciona una empresa en modo soporte para ver el diagnóstico.";
            return RedirectToAction("Index", "Soporte");
        }
        return RedirectToAction("Index", "Home");
    }
}
