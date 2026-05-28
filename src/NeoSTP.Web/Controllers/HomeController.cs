using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Dashboard;
using NeoSTP.Application.Empresas;
using NeoSTP.Web.Models;

namespace NeoSTP.Web.Controllers;

[Authorize]
public class HomeController : Controller
{
    private readonly ICurrentUser _currentUser;
    private readonly IDashboardService _dashboard;
    private readonly IEmpresaContext _empresaContext;
    private readonly IEmpresasService _empresas;

    public HomeController(
        ICurrentUser currentUser,
        IDashboardService dashboard,
        IEmpresaContext empresaContext,
        IEmpresasService empresas)
    {
        _currentUser = currentUser;
        _dashboard = dashboard;
        _empresaContext = empresaContext;
        _empresas = empresas;
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

        var vm = new DashboardViewModel
        {
            Username = _currentUser.Username ?? "",
            EsSuperAdmin = isSuperAdmin,
            EmpresaDashboard = dto,
            EmpresaNombre = empNombre,
        };

        return View(vm);
    }

    [AllowAnonymous]
    public IActionResult Privacy() => View();

    [AllowAnonymous]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
        => View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
}
