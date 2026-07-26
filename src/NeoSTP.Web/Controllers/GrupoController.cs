using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Dashboard;

namespace NeoSTP.Web.Controllers;

/// <summary>
/// Consolidado de grupo (E5): ventas, IVA, cartera y alertas de todas las empresas
/// donde el usuario puede operar. El alcance sale de las membresías (E1), así que
/// no necesita permiso adicional: cada quien ve solo lo suyo.
/// </summary>
[Authorize]
public class GrupoController : Controller
{
    private readonly IGrupoDashboardService _grupo;
    private readonly ICurrentUser _currentUser;

    public GrupoController(IGrupoDashboardService grupo, ICurrentUser currentUser)
    {
        _grupo = grupo;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<IActionResult> Index(int? anio, int? mes, CancellationToken ct)
    {
        if (_currentUser.UserId is not int userId) return Forbid();

        var result = await _grupo.GetAsync(userId, anio, mes, ct);
        if (result.IsFailure)
        {
            TempData["Error"] = result.Error;
            return RedirectToAction("Index", "Home");
        }

        // Con una sola empresa el consolidado no aporta nada: el dashboard normal ya la cubre.
        if (result.Value!.Empresas.Count < 2)
        {
            TempData["Error"] = "El consolidado de grupo requiere acceso a más de una empresa.";
            return RedirectToAction("Index", "Home");
        }

        return View(result.Value);
    }
}
