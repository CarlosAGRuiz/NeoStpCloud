using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Common;
using NeoSTP.Application.Empresas;

namespace NeoSTP.Web.Controllers;

/// <summary>
/// Modo soporte: solo SuperAdmin. Permite "operar como" una empresa específica
/// para usar las pantallas per-tenant (Clientes, Productos, Config DTE).
/// La selección se persiste en cookie HTTP-only por 7 días.
/// </summary>
[Authorize]
public class SoporteController : Controller
{
    private readonly IEmpresaContext _empresaContext;
    private readonly IEmpresasService _empresas;
    private readonly ICurrentUser _currentUser;

    public SoporteController(IEmpresaContext empresaContext, IEmpresasService empresas, ICurrentUser currentUser)
    {
        _empresaContext = empresaContext;
        _empresas = empresas;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? search, CancellationToken ct)
    {
        if (_currentUser.TipoUsuarioCodigo != "SUPERADMIN") return Forbid();
        var result = await _empresas.GetListAsync(null, new PagedQuery { Search = search, Page = 1, PageSize = 100 }, ct);
        ViewBag.Search = search;
        ViewBag.CurrentEmpresaId = _empresaContext.CurrentEmpresaId;
        return View(result.Value);
    }

    [HttpGet("Soporte/Entrar/{id:int}")]
    public IActionResult EntrarGet(int id) => RedirectToAction(nameof(Index));

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Entrar(int id, string? returnUrl, CancellationToken ct)
    {
        if (_currentUser.TipoUsuarioCodigo != "SUPERADMIN") return Forbid();

        var result = await _empresas.GetByIdAsync(null, id, ct);
        if (result.IsFailure) return NotFound();

        _empresaContext.SetSupportEmpresa(result.Value!.Id, result.Value.RazonSocial);
        TempData["Success"] = $"Modo soporte activo: {result.Value.RazonSocial}";

        return Url.IsLocalUrl(returnUrl) ? Redirect(returnUrl!) : RedirectToAction("Index", "Home");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Salir()
    {
        _empresaContext.ClearSupportEmpresa();
        TempData["Success"] = "Modo soporte desactivado.";
        return RedirectToAction("Index", "Home");
    }
}
