using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Empresas;
using NeoSTP.Application.Profit;
using NeoSTP.Application.Profit.Dtos;

namespace NeoSTP.Web.Controllers;

/// <summary>
/// NeoProfit — dashboard financiero (ventas, IVA, rentabilidad, rankings).
/// Opera dentro de una empresa; SuperAdmin debe entrar en modo soporte.
/// </summary>
[Authorize]
public class ProfitController : Controller
{
    private readonly IProfitService _profit;
    private readonly ICurrentUser _currentUser;
    private readonly IEmpresaContext _empresaContext;

    public ProfitController(IProfitService profit, ICurrentUser currentUser, IEmpresaContext empresaContext)
    {
        _profit = profit;
        _currentUser = currentUser;
        _empresaContext = empresaContext;
    }

    [HttpGet]
    public async Task<IActionResult> Index(DateOnly? desde, DateOnly? hasta, CancellationToken ct)
    {
        if (!Has("Profit.Ver")) return Forbid();
        if (RequireEmpresa() is not int eid) return RedirectToSoporte();

        var periodo = new ProfitPeriodoQuery { Desde = desde, Hasta = hasta };
        var dashboard = await _profit.GetDashboardAsync(eid, periodo, ct);
        ViewBag.PuedeGestionar = Has("Profit.Gestionar");
        return View(dashboard);
    }

    private bool Has(string codigo)
        => _currentUser.TipoUsuarioCodigo == "SUPERADMIN" || _currentUser.HasPermiso(codigo);

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
