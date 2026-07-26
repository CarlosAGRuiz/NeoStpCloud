using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NeoSTP.Web.Auth;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Empresas;
using NeoSTP.Application.Reportes;

namespace NeoSTP.Web.Controllers;

/// <summary>NEOBI fiscal â€” libros IVA + F-07. Permiso Reportes.Ver.</summary>
[Authorize]
[RequireModulo("NEOBI")]
public class NeoBiController : Controller
{
    private readonly IReporteFiscalService _reportes;
    private readonly ICurrentUser _currentUser;
    private readonly IEmpresaContext _empresaContext;

    public NeoBiController(IReporteFiscalService reportes, ICurrentUser currentUser, IEmpresaContext empresaContext)
    {
        _reportes = reportes;
        _currentUser = currentUser;
        _empresaContext = empresaContext;
    }

    [HttpGet]
    public async Task<IActionResult> Index(int? anio, int? mes, CancellationToken ct)
    {
        if (!Has("Reportes.Ver")) return Forbid();
        if (RequireEmpresa() is not int eid) return RedirectToSoporte();

        var hoy = DateTime.UtcNow;
        var a = anio ?? hoy.Year;
        var m = mes ?? hoy.Month;

        var consumidor = await _reportes.LibroVentasConsumidorAsync(eid, a, m, ct);
        var contribuyentes = await _reportes.LibroVentasContribuyentesAsync(eid, a, m, ct);
        var compras = await _reportes.LibroComprasAsync(eid, a, m, ct);
        var f07 = await _reportes.ResumenF07Async(eid, a, m, ct);

        ViewBag.Anio = a;
        ViewBag.Mes = m;
        ViewBag.Consumidor = consumidor.Value;
        ViewBag.Contribuyentes = contribuyentes.Value;
        ViewBag.Compras = compras.Value;
        return View(f07.Value);
    }

    [HttpGet]
    public async Task<IActionResult> Csv(string libro, int anio, int mes, CancellationToken ct)
    {
        if (!Has("Reportes.Ver")) return Forbid();
        if (RequireEmpresa() is not int eid) return RedirectToSoporte();

        var r = libro switch
        {
            "consumidor" => await _reportes.LibroVentasConsumidorCsvAsync(eid, anio, mes, ct),
            "contribuyentes" => await _reportes.LibroVentasContribuyentesCsvAsync(eid, anio, mes, ct),
            "compras" => await _reportes.LibroComprasCsvAsync(eid, anio, mes, ct),
            _ => NeoSTP.Application.Common.Result<byte[]>.Fail("Libro invÃ¡lido.", "VALIDATION"),
        };
        if (r.IsFailure)
        {
            TempData["Error"] = r.Error;
            return RedirectToAction(nameof(Index), new { anio, mes });
        }
        return File(r.Value!, "text/csv", $"libro_{libro}_{anio:0000}_{mes:00}.csv");
    }

    private bool Has(string codigo) => _currentUser.TipoUsuarioCodigo == "SUPERADMIN" || _currentUser.HasPermiso(codigo);
    private int? RequireEmpresa() => _empresaContext.CurrentEmpresaId;

    private IActionResult RedirectToSoporte()
    {
        if (_currentUser.TipoUsuarioCodigo == "SUPERADMIN")
        {
            TempData["Error"] = "Los reportes fiscales operan dentro de una empresa. Selecciona una en modo soporte primero.";
            return RedirectToAction("Index", "Soporte");
        }
        return RedirectToAction("Index", "Home");
    }
}
