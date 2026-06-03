using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Empresas;
using NeoSTP.Application.Onboarding;

namespace NeoSTP.Web.Controllers;

/// <summary>
/// Asistente de bienvenida (onboarding self-service). Presenta los pasos de activación
/// guiados; reusa <see cref="IOnboardingService"/> como única fuente de verdad.
/// Acceso manual (no fuerza redirección post-login).
/// </summary>
[Authorize]
[Route("onboarding")]
public class OnboardingController : Controller
{
    private readonly IOnboardingService _onboarding;
    private readonly ICurrentUser _currentUser;
    private readonly IEmpresaContext _empresaContext;

    public OnboardingController(
        IOnboardingService onboarding,
        ICurrentUser currentUser,
        IEmpresaContext empresaContext)
    {
        _onboarding = onboarding;
        _currentUser = currentUser;
        _empresaContext = empresaContext;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        if (_empresaContext.CurrentEmpresaId is not int eid)
        {
            if (_currentUser.TipoUsuarioCodigo == "SUPERADMIN")
            {
                TempData["Error"] = "El asistente opera dentro de una empresa. Selecciona una en modo soporte primero.";
                return RedirectToAction("Index", "Soporte");
            }
            return RedirectToAction("Index", "Home");
        }

        var estado = await _onboarding.GetEstadoAsync(eid, ct);
        return View(estado);
    }
}
