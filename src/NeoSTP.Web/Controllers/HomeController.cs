using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Dashboard;
using NeoSTP.Application.Empresas;
using NeoSTP.Application.Onboarding;
using NeoSTP.Web.Models;

namespace NeoSTP.Web.Controllers;

[Authorize]
public class HomeController : Controller
{
    private readonly ICurrentUser _currentUser;
    private readonly IDashboardService _dashboard;
    private readonly IEmpresaContext _empresaContext;
    private readonly IEmpresasService _empresas;
    private readonly IOnboardingService _onboarding;
    private readonly NeoSTP.Application.Cobranza.ICobranzaService _cobranza;
    private readonly NeoSTP.Application.Tesoreria.ITesoreriaService _tesoreria;
    private readonly NeoSTP.Application.Notificaciones.IAlertaService _alertas;

    public HomeController(
        ICurrentUser currentUser,
        IDashboardService dashboard,
        IEmpresaContext empresaContext,
        IEmpresasService empresas,
        IOnboardingService onboarding,
        NeoSTP.Application.Cobranza.ICobranzaService cobranza,
        NeoSTP.Application.Tesoreria.ITesoreriaService tesoreria,
        NeoSTP.Application.Notificaciones.IAlertaService alertas)
    {
        _currentUser = currentUser;
        _dashboard = dashboard;
        _empresaContext = empresaContext;
        _empresas = empresas;
        _onboarding = onboarding;
        _cobranza = cobranza;
        _tesoreria = tesoreria;
        _alertas = alertas;
    }

    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var isSuperAdmin = _currentUser.TipoUsuarioCodigo == "SUPERADMIN";
        var empresaId = _empresaContext.CurrentEmpresaId;

        // SuperAdmin sin empresa en contexto → panel global
        if (isSuperAdmin && empresaId is null)
        {
            var saDto = await _dashboard.GetDashboardSuperAdminAsync(ct);
            var saVm = new DashboardViewModel
            {
                Username = _currentUser.Username ?? "SuperAdmin",
                EsSuperAdmin = true,
                SuperAdminDashboard = saDto,
            };
            return View("SuperAdmin", saVm);
        }

        // Usuario de empresa o SuperAdmin en modo soporte
        if (empresaId is null)
        {
            // Sin empresa asignada — mostrar dashboard vacío (edge-case)
            return View(new DashboardViewModel { Username = _currentUser.Username ?? "" });
        }

        var dto = await _dashboard.GetDashboardEmpresaAsync(empresaId.Value, ct);

        // Obtener nombre de la empresa para el título
        string? empNombre = _empresaContext.SupportEmpresaNombre;
        if (empNombre is null)
        {
            var emp = await _empresas.GetByIdAsync(null, empresaId.Value, ct);
            empNombre = emp.Value?.RazonSocial;
        }

        var onboarding = await _onboarding.GetEstadoAsync(empresaId.Value, ct);

        var vm = new DashboardViewModel
        {
            Username = _currentUser.Username ?? "",
            EsSuperAdmin = isSuperAdmin,
            EmpresaDashboard = dto,
            EmpresaNombre = empNombre,
            Onboarding = onboarding,
        };

        // KPIs de negocio adicionales, condicionados al permiso de cada módulo.
        bool Puede(string p) => isSuperAdmin || _currentUser.HasPermiso(p);
        if (Puede("Cobros.Ver"))
            vm.Cobranza = await _cobranza.GetResumenAsync(empresaId.Value, ct);
        if (Puede("Tesoreria.Cuentas.Ver"))
            vm.Tesoreria = (await _tesoreria.ResumenAsync(empresaId.Value, ct)).Value;
        if (_currentUser.UserId is int uid)
            vm.AlertasActivas = (await _alertas.ResumenAsync(empresaId.Value, uid, ct)).Pendientes;

        return View(vm);
    }

    [AllowAnonymous]
    public IActionResult Privacy() => View();

    /// <summary>V2.5-S6 — cambia el idioma de la UI (es/en) vía cookie de cultura estándar.</summary>
    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult CambiarIdioma(string cultura, string? returnUrl)
    {
        if (cultura is "es" or "en")
        {
            Response.Cookies.Append(
                Microsoft.AspNetCore.Localization.CookieRequestCultureProvider.DefaultCookieName,
                Microsoft.AspNetCore.Localization.CookieRequestCultureProvider.MakeCookieValue(
                    new Microsoft.AspNetCore.Localization.RequestCulture(cultura)),
                new CookieOptions { Expires = DateTimeOffset.UtcNow.AddYears(1), IsEssential = true, HttpOnly = false });
        }
        return Url.IsLocalUrl(returnUrl) ? Redirect(returnUrl!) : RedirectToAction(nameof(Index));
    }

    [AllowAnonymous]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
        => View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
}
