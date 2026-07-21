using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using NeoSTP.Application.Auth;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Auth.Dtos;
using NeoSTP.Application.Roles;
using NeoSTP.Domain.Core.Seguridad;

namespace NeoSTP.Web.Controllers;

/// <summary>
/// Configuración de SSO corporativo (E3) por empresa: proveedor OIDC, dominio,
/// auto-aprovisionamiento y rol por defecto. Requiere Seguridad.Sso.Gestionar.
/// </summary>
[Authorize]
public class SsoController : Controller
{
    private readonly ISsoConfigService _sso;
    private readonly IRolesService _roles;
    private readonly ICurrentUser _currentUser;
    private readonly NeoSTP.Application.Empresas.IEmpresaContext _empresaContext;
    private readonly SsoOptions _ssoOptions;

    public SsoController(
        ISsoConfigService sso,
        IRolesService roles,
        ICurrentUser currentUser,
        NeoSTP.Application.Empresas.IEmpresaContext empresaContext,
        IOptions<SsoOptions> ssoOptions)
    {
        _sso = sso;
        _roles = roles;
        _currentUser = currentUser;
        _empresaContext = empresaContext;
        _ssoOptions = ssoOptions.Value;
    }

    private int? Empresa => _empresaContext.CurrentEmpresaId;

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        if (!HasPermiso("Seguridad.Sso.Gestionar")) return Forbid();
        if (Empresa is not int eid) return Forbid();

        var config = await _sso.GetAsync(eid, ct);
        await CargarViewBagAsync(eid, ct);
        return View(config.Value);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Guardar(GuardarEmpresaSsoRequest model, CancellationToken ct)
    {
        if (!HasPermiso("Seguridad.Sso.Gestionar")) return Forbid();
        if (Empresa is not int eid) return Forbid();

        var result = await _sso.GuardarAsync(eid, model, _currentUser.Username, ct);
        if (result.IsFailure)
        {
            TempData["Error"] = result.Error;
            return RedirectToAction(nameof(Index));
        }

        TempData["Success"] = "Configuración de SSO guardada.";
        return RedirectToAction(nameof(Index));
    }

    private async Task CargarViewBagAsync(int empresaId, CancellationToken ct)
    {
        ViewBag.Roles = (await _roles.GetListAsync(empresaId, ct)).Value
            ?? new List<NeoSTP.Application.Roles.Dtos.RolDto>();
        ViewBag.Proveedores = SsoProveedores.All;
        // Estado global: si el SaaS aún no tiene credenciales OIDC, el SSO no operará
        // aunque la empresa lo habilite. Se avisa en la UI.
        ViewBag.SsoGlobalHabilitado = _ssoOptions.Enabled;
        ViewBag.MicrosoftListo = _ssoOptions.Microsoft.IsConfigured;
        ViewBag.GoogleListo = _ssoOptions.Google.IsConfigured;
    }

    private bool HasPermiso(string codigo)
        => _currentUser.TipoUsuarioCodigo == "SUPERADMIN" || _currentUser.HasPermiso(codigo);
}
