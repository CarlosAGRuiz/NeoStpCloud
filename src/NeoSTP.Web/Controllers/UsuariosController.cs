using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Common;
using NeoSTP.Application.Roles;
using NeoSTP.Application.Usuarios;
using NeoSTP.Application.Usuarios.Dtos;
using NeoSTP.Web.Models;

namespace NeoSTP.Web.Controllers;

[Authorize]
public class UsuariosController : Controller
{
    private readonly IUsuariosService _usuarios;
    private readonly IRolesService _roles;
    private readonly ICurrentUser _currentUser;
    private readonly NeoSTP.Application.Empresas.IEmpresaContext _empresaContext;

    public UsuariosController(
        IUsuariosService usuarios,
        IRolesService roles,
        ICurrentUser currentUser,
        NeoSTP.Application.Empresas.IEmpresaContext empresaContext)
    {
        _usuarios = usuarios;
        _roles = roles;
        _currentUser = currentUser;
        _empresaContext = empresaContext;
    }

    /// <summary>EmpresaId efectivo: si SuperAdmin en modo soporte, lo de cookie; si no, el del usuario.</summary>
    private int? Empresa => _empresaContext.CurrentEmpresaId;

    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] string? search, [FromQuery] int page = 1, CancellationToken ct = default)
    {
        if (!HasPermiso("Core.Usuarios.Ver")) return Forbid();

        var result = await _usuarios.GetListAsync(Empresa,
            new PagedQuery { Search = search, Page = page, PageSize = 20 }, ct);
        ViewBag.Search = search;
        return View(result.Value);
    }

    [HttpGet]
    public async Task<IActionResult> Create(CancellationToken ct)
    {
        if (!HasPermiso("Core.Usuarios.Crear")) return Forbid();
        await LoadRolesAsync(ct);
        return View(new CreateUsuarioViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateUsuarioViewModel model, CancellationToken ct)
    {
        if (!HasPermiso("Core.Usuarios.Crear")) return Forbid();
        if (!ModelState.IsValid)
        {
            await LoadRolesAsync(ct);
            return View(model);
        }

        var result = await _usuarios.CreateAsync(Empresa, new CreateUsuarioRequest
        {
            Username = model.Username,
            Email = model.Email,
            Password = model.Password,
            NombreCompleto = model.NombreCompleto,
            Telefono = model.Telefono,
            TipoUsuarioCodigo = model.TipoUsuarioCodigo,
            RoleIds = model.RoleIds,
        }, _currentUser.Username, ct);

        if (result.IsFailure)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Error.");
            foreach (var e in result.ValidationErrors) ModelState.AddModelError(string.Empty, e);
            await LoadRolesAsync(ct);
            return View(model);
        }

        TempData["Success"] = $"Usuario {result.Value!.Username} creado.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id, CancellationToken ct)
    {
        if (!HasPermiso("Core.Usuarios.Editar")) return Forbid();

        var result = await _usuarios.GetByIdAsync(Empresa, id, ct);
        if (result.IsFailure) return NotFound();

        await LoadRolesAsync(ct);
        var u = result.Value!;
        return View(new EditUsuarioViewModel
        {
            Id = u.Id,
            Username = u.Username,
            Email = u.Email,
            NombreCompleto = u.NombreCompleto,
            Telefono = u.Telefono,
            TipoUsuarioCodigo = u.TipoUsuarioCodigo,
            EstadoCodigo = u.EstadoCodigo,
            RoleIds = u.RoleIds.ToArray(),
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(EditUsuarioViewModel model, CancellationToken ct)
    {
        if (!HasPermiso("Core.Usuarios.Editar")) return Forbid();
        if (!ModelState.IsValid)
        {
            await LoadRolesAsync(ct);
            return View(model);
        }

        var result = await _usuarios.UpdateAsync(Empresa, model.Id, new UpdateUsuarioRequest
        {
            Email = model.Email,
            NombreCompleto = model.NombreCompleto,
            Telefono = model.Telefono,
            TipoUsuarioCodigo = model.TipoUsuarioCodigo,
            EstadoCodigo = model.EstadoCodigo,
            RoleIds = model.RoleIds,
        }, _currentUser.Username, ct);

        if (result.IsFailure)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Error.");
            await LoadRolesAsync(ct);
            return View(model);
        }

        TempData["Success"] = "Usuario actualizado.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Bloquear(int id, CancellationToken ct)
    {
        if (!HasPermiso("Core.Usuarios.Bloquear")) return Forbid();
        var result = await _usuarios.BloquearAsync(Empresa, id, _currentUser.Username, ct);
        TempData[result.IsSuccess ? "Success" : "Error"] = result.IsSuccess ? "Usuario bloqueado." : result.Error;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Desbloquear(int id, CancellationToken ct)
    {
        if (!HasPermiso("Core.Usuarios.Bloquear")) return Forbid();
        var result = await _usuarios.DesbloquearAsync(Empresa, id, _currentUser.Username, ct);
        TempData[result.IsSuccess ? "Success" : "Error"] = result.IsSuccess ? "Usuario desbloqueado." : result.Error;
        return RedirectToAction(nameof(Index));
    }

    private bool HasPermiso(string codigo)
        => _currentUser.TipoUsuarioCodigo == "SUPERADMIN" || _currentUser.HasPermiso(codigo);

    private async Task LoadRolesAsync(CancellationToken ct)
    {
        var rolesResult = await _roles.GetListAsync(Empresa, ct);
        ViewBag.RolesDisponibles = rolesResult.Value ?? new List<NeoSTP.Application.Roles.Dtos.RolDto>();
    }
}
