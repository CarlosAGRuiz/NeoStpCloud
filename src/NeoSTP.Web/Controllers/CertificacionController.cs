using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Dte.Certificacion;
using NeoSTP.Application.Dte.Certificacion.Dtos;
using NeoSTP.Application.Empresas;

namespace NeoSTP.Web.Controllers;

[Authorize]
public class CertificacionController : Controller
{
    private readonly ICertificacionDteService _service;
    private readonly ICurrentUser _currentUser;
    private readonly IEmpresaContext _empresaContext;

    public CertificacionController(ICertificacionDteService service, ICurrentUser currentUser, IEmpresaContext empresaContext)
    {
        _service = service;
        _currentUser = currentUser;
        _empresaContext = empresaContext;
    }

    // ----- Dashboard -----

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        if (!Has("Core.Certificacion.Ver")) return Forbid();
        if (RequireEmpresa() is not int eid) return RedirectToSoporte();

        var resumen = await _service.GetResumenAsync(eid, ct);
        var matriz = await _service.GetMatrizAsync(eid, ct);

        ViewBag.Resumen = resumen.Value;
        ViewBag.PuedeOperar = Has("Core.Certificacion.Operar");
        return View(matriz.Value ?? Array.Empty<CertificacionMatrizProgresoDto>());
    }

    [HttpGet]
    public async Task<IActionResult> Matriz(CancellationToken ct)
    {
        if (!Has("Core.Certificacion.Ver")) return Forbid();
        if (RequireEmpresa() is not int eid) return RedirectToSoporte();

        var result = await _service.GetMatrizAsync(eid, ct);
        ViewBag.PuedeOperar = Has("Core.Certificacion.Operar");
        return View(result.Value ?? Array.Empty<CertificacionMatrizProgresoDto>());
    }

    [HttpGet("Certificacion/Tipo/{codigo}")]
    public async Task<IActionResult> Tipo(string codigo, CancellationToken ct)
    {
        if (!Has("Core.Certificacion.Ver")) return Forbid();
        if (RequireEmpresa() is not int eid) return RedirectToSoporte();

        var escenarios = await _service.GetEscenariosAsync(codigo, eid, ct);
        if (escenarios.IsFailure) return NotFound();

        var matriz = await _service.GetMatrizAsync(eid, ct);
        var fila = matriz.Value?.FirstOrDefault(m => string.Equals(m.TipoDteCodigo, codigo, StringComparison.OrdinalIgnoreCase));

        ViewBag.TipoCodigo = codigo.ToUpperInvariant();
        ViewBag.Progreso = fila;
        ViewBag.PuedeOperar = Has("Core.Certificacion.Operar");
        return View(escenarios.Value ?? Array.Empty<CertificacionEscenarioDto>());
    }

    [HttpGet]
    public async Task<IActionResult> Errores([FromQuery] string? codigoMh, CancellationToken ct)
    {
        if (!Has("Core.Certificacion.Ver")) return Forbid();
        if (RequireEmpresa() is not int eid) return RedirectToSoporte();

        var result = await _service.GetErroresAsync(eid, codigoMh, ct);
        ViewBag.Filtro = codigoMh;
        return View(result.Value ?? Array.Empty<CertificacionErrorDto>());
    }

    // ----- Acciones -----

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GenerarPrueba(string codigo, CancellationToken ct)
    {
        if (!Has("Core.Certificacion.Operar")) return Forbid();
        if (RequireEmpresa() is not int eid) return RedirectToSoporte();

        var result = await _service.GenerarPruebaAsync(codigo, eid, _currentUser.Username, ct);
        TempData[result.IsSuccess ? "Success" : "Error"] = result.IsSuccess
            ? $"Prueba abierta para {result.Value!.EscenarioCodigo} (intento {result.Value.IntentoNumero})."
            : result.Error;
        return RedirectToAction(nameof(Tipo), new { codigo });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reintentar(int documentoId, string codigo, CancellationToken ct)
    {
        if (!Has("Core.Certificacion.Operar")) return Forbid();
        if (RequireEmpresa() is not int eid) return RedirectToSoporte();

        var result = await _service.ReintentarAsync(documentoId, eid, _currentUser.Username, ct);
        TempData[result.IsSuccess ? "Success" : "Error"] = result.IsSuccess
            ? $"Reintento abierto (intento {result.Value!.IntentoNumero})."
            : result.Error;
        return RedirectToAction(nameof(Tipo), new { codigo });
    }

    // ----- Helpers -----

    private bool Has(string codigo)
        => _currentUser.TipoUsuarioCodigo == "SUPERADMIN" || _currentUser.HasPermiso(codigo);

    private int? RequireEmpresa() => _empresaContext.CurrentEmpresaId;

    private IActionResult RedirectToSoporte()
    {
        if (_currentUser.TipoUsuarioCodigo == "SUPERADMIN")
        {
            TempData["Error"] = "Selecciona una empresa en modo soporte para ver su matriz de certificación.";
            return RedirectToAction("Index", "Soporte");
        }
        return RedirectToAction("Index", "Home");
    }
}
