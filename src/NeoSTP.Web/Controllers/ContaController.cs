using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NeoSTP.Web.Auth;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Common;
using NeoSTP.Application.Conta;
using NeoSTP.Application.Empresas;

namespace NeoSTP.Web.Controllers;

/// <summary>NEOCONTA â€” balanza y asientos. Permisos Conta.Ver / Conta.Gestionar.</summary>
[Authorize]
[RequireModulo("NEOCONTA")]
public class ContaController : Controller
{
    private readonly IContabilidadService _conta;
    private readonly ICurrentUser _currentUser;
    private readonly IEmpresaContext _empresaContext;

    public ContaController(IContabilidadService conta, ICurrentUser currentUser, IEmpresaContext empresaContext)
    {
        _conta = conta;
        _currentUser = currentUser;
        _empresaContext = empresaContext;
    }

    [HttpGet]
    public async Task<IActionResult> Index(int? anio, int? mes, int page = 1, CancellationToken ct = default)
    {
        if (!Has("Conta.Ver")) return Forbid();
        if (RequireEmpresa() is not int eid) return RedirectToSoporte();

        var hoy = DateTime.UtcNow;
        var a = anio ?? hoy.Year;
        var m = mes ?? hoy.Month;

        var balanza = await _conta.BalanzaAsync(eid, a, m, ct);
        var asientos = await _conta.ListAsientosAsync(eid, a, m, new PagedQuery { Page = page, PageSize = 20 }, ct);
        ViewBag.Anio = a;
        ViewBag.Mes = m;
        ViewBag.Asientos = asientos.Value;
        ViewBag.PuedeGestionar = Has("Conta.Gestionar");
        return View(balanza.Value);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Generar(int anio, int mes, CancellationToken ct)
    {
        if (!Has("Conta.Gestionar")) return Forbid();
        if (RequireEmpresa() is not int eid) return RedirectToSoporte();
        var r = await _conta.GenerarAsientosPeriodoAsync(eid, anio, mes, _currentUser.Username, ct);
        TempData[r.IsSuccess ? "Success" : "Error"] = r.IsSuccess
            ? $"Asientos generados: {r.Value} nuevo(s)."
            : r.Error;
        return RedirectToAction(nameof(Index), new { anio, mes });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reversar(int id, string? motivo, int anio, int mes, CancellationToken ct)
    {
        if (!Has("Conta.Gestionar")) return Forbid();
        if (RequireEmpresa() is not int eid) return RedirectToSoporte();
        var r = await _conta.ReversarAsientoAsync(eid, id, motivo, _currentUser.Username, ct);
        TempData[r.IsSuccess ? "Success" : "Error"] = r.IsSuccess ? $"Reversa creada: {r.Value!.Numero}." : r.Error;
        return RedirectToAction(nameof(Index), new { anio, mes });
    }

    [HttpGet]
    public async Task<IActionResult> BalanzaCsv(int anio, int mes, CancellationToken ct)
    {
        if (!Has("Conta.Ver")) return Forbid();
        if (RequireEmpresa() is not int eid) return RedirectToSoporte();
        var r = await _conta.BalanzaCsvAsync(eid, anio, mes, ct);
        if (r.IsFailure)
        {
            TempData["Error"] = r.Error;
            return RedirectToAction(nameof(Index), new { anio, mes });
        }
        return File(r.Value!, "text/csv", $"balanza_{anio:0000}_{mes:00}.csv");
    }

    private bool Has(string codigo) => _currentUser.TipoUsuarioCodigo == "SUPERADMIN" || _currentUser.HasPermiso(codigo);
    private int? RequireEmpresa() => _empresaContext.CurrentEmpresaId;

    private IActionResult RedirectToSoporte()
    {
        if (_currentUser.TipoUsuarioCodigo == "SUPERADMIN")
        {
            TempData["Error"] = "La contabilidad opera dentro de una empresa. Selecciona una en modo soporte primero.";
            return RedirectToAction("Index", "Soporte");
        }
        return RedirectToAction("Index", "Home");
    }
}
