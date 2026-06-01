using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Dte.Contingencia;
using NeoSTP.Application.Empresas;

namespace NeoSTP.Web.Controllers;

[Authorize]
public class DteContingenciaController : Controller
{
    private readonly IContingenciaLoteService _service;
    private readonly ICurrentUser _currentUser;
    private readonly IEmpresaContext _empresaContext;

    public DteContingenciaController(
        IContingenciaLoteService service,
        ICurrentUser currentUser,
        IEmpresaContext empresaContext)
    {
        _service = service;
        _currentUser = currentUser;
        _empresaContext = empresaContext;
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Cola de contingencia — documentos pendientes y resumen
    // ──────────────────────────────────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        if (!Has("DTE.Contingencia")) return Forbid();
        if (RequireEmpresa() is not int eid) return RedirectToSoporte();

        var resumen = await _service.ObtenerResumenAsync(eid, ct);
        var documentos = await _service.ListarDocumentosPendientesAsync(eid, ct);

        ViewBag.Resumen = resumen;
        return View(documentos);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Lotes
    // ──────────────────────────────────────────────────────────────────────────

    [HttpGet("DteContingencia/Lotes")]
    public async Task<IActionResult> Lotes(CancellationToken ct)
    {
        if (!Has("DTE.Contingencia")) return Forbid();
        if (RequireEmpresa() is not int eid) return RedirectToSoporte();

        var lotes = await _service.ListarLotesAsync(eid, ct);
        return View(lotes);
    }

    [HttpGet("DteContingencia/Lotes/{id:int}")]
    public async Task<IActionResult> DetalleLote(int id, CancellationToken ct)
    {
        if (!Has("DTE.Contingencia")) return Forbid();
        if (RequireEmpresa() is not int eid) return RedirectToSoporte();

        var lote = await _service.ObtenerLoteAsync(id, eid, ct);
        if (lote is null) return NotFound();
        return View(lote);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Acciones POST
    // ──────────────────────────────────────────────────────────────────────────

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CrearLote(int eventoContingenciaId, CancellationToken ct)
    {
        if (!Has("DTE.Contingencia")) return Forbid();
        if (RequireEmpresa() is not int eid) return RedirectToSoporte();

        var result = await _service.CrearYEnviarLoteAsync(
            eventoContingenciaId, eid, _currentUser.Username ?? "web", ct);

        if (result.IsFailure)
        {
            TempData["Error"] = result.Error;
            return RedirectToAction(nameof(Index));
        }

        TempData["Success"] = $"Lote enviado correctamente. CodigoLote: {result.Value!.CodigoLote}";
        return RedirectToAction(nameof(DetalleLote), new { id = result.Value.LoteId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ConsultarLote(int loteId, CancellationToken ct)
    {
        if (!Has("DTE.Contingencia")) return Forbid();
        if (RequireEmpresa() is not int eid) return RedirectToSoporte();

        var result = await _service.ConsultarLoteAsync(loteId, eid, ct);

        if (result.IsFailure)
            TempData["Error"] = result.Error;
        else
            TempData["Success"] = result.Value!.Mensaje;

        return RedirectToAction(nameof(DetalleLote), new { id = loteId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReintentarDocumento(int dteId, CancellationToken ct)
    {
        if (!Has("DTE.Contingencia")) return Forbid();
        if (RequireEmpresa() is not int eid) return RedirectToSoporte();

        var result = await _service.ReintentarDocumentoAsync(dteId, eid, ct);

        if (result.IsFailure)
            TempData["Error"] = result.Error;
        else
            TempData["Success"] = result.Value;

        return RedirectToAction(nameof(Index));
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────────────────────

    private bool Has(string codigo)
        => _currentUser.TipoUsuarioCodigo == "SUPERADMIN" || _currentUser.HasPermiso(codigo);

    private int? RequireEmpresa() => _empresaContext.CurrentEmpresaId;

    private IActionResult RedirectToSoporte()
    {
        if (_currentUser.TipoUsuarioCodigo == "SUPERADMIN")
        {
            TempData["Error"] = "Selecciona una empresa en modo soporte para gestionar contingencia.";
            return RedirectToAction("Index", "Soporte");
        }
        return RedirectToAction("Index", "Home");
    }
}
