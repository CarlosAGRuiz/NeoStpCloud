using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Comunicaciones;
using NeoSTP.Application.Empresas;

namespace NeoSTP.Web.Controllers;

/// <summary>Configuración de correo saliente (SMTP) por empresa. Permiso Core.Correo.Configurar.</summary>
[Authorize]
public class CorreoController : Controller
{
    private readonly IConfiguracionCorreoService _correo;
    private readonly ICurrentUser _currentUser;
    private readonly IEmpresaContext _empresaContext;

    public CorreoController(IConfiguracionCorreoService correo, ICurrentUser currentUser, IEmpresaContext empresaContext)
    {
        _correo = correo;
        _currentUser = currentUser;
        _empresaContext = empresaContext;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        if (!Has("Core.Correo.Configurar")) return Forbid();
        if (RequireEmpresa() is not int eid) return RedirectToSoporte();

        var result = await _correo.GetAsync(eid, ct);
        return View(result.Value);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Guardar(GuardarConfiguracionCorreoRequest model, CancellationToken ct)
    {
        if (!Has("Core.Correo.Configurar")) return Forbid();
        if (RequireEmpresa() is not int eid) return RedirectToSoporte();

        var result = await _correo.GuardarAsync(eid, model, _currentUser.Username, ct);
        if (result.IsFailure)
        {
            foreach (var e in result.ValidationErrors ?? new[] { result.Error ?? "Error." }) ModelState.AddModelError(string.Empty, e);
            return View(nameof(Index), result.Value);
        }
        TempData["Success"] = "Configuración de correo guardada.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Probar(string destino, CancellationToken ct)
    {
        if (!Has("Core.Correo.Configurar")) return Forbid();
        if (RequireEmpresa() is not int eid) return RedirectToSoporte();

        var result = await _correo.ProbarAsync(eid, destino, _currentUser.Username, ct);
        TempData[result.IsSuccess ? "Success" : "Error"] = result.IsSuccess ? $"Correo de prueba enviado a {destino}." : result.Error;
        return RedirectToAction(nameof(Index));
    }

    private bool Has(string codigo) => _currentUser.TipoUsuarioCodigo == "SUPERADMIN" || _currentUser.HasPermiso(codigo);
    private int? RequireEmpresa() => _empresaContext.CurrentEmpresaId;

    private IActionResult RedirectToSoporte()
    {
        if (_currentUser.TipoUsuarioCodigo == "SUPERADMIN")
        {
            TempData["Error"] = "La configuración de correo opera dentro de una empresa. Selecciona una en modo soporte primero.";
            return RedirectToAction("Index", "Soporte");
        }
        return RedirectToAction("Index", "Home");
    }
}
