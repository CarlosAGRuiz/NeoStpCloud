using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Common;
using NeoSTP.Application.Empresas;
using NeoSTP.Application.Empresas.Dtos;
using NeoSTP.Web.Models;

namespace NeoSTP.Web.Controllers;

[Authorize]
public class EmpresasController : Controller
{
    private readonly IEmpresasService _empresas;
    private readonly ICurrentUser _currentUser;
    private readonly NeoSTP.Application.Lookups.ILookupService _lookups;

    public EmpresasController(IEmpresasService empresas, ICurrentUser currentUser, NeoSTP.Application.Lookups.ILookupService lookups)
    {
        _empresas = empresas;
        _currentUser = currentUser;
        _lookups = lookups;
    }

    private async Task CargarTerritorialAsync(CancellationToken ct)
    {
        ViewBag.Departamentos = await _lookups.GetDepartamentosAsync(_currentUser.EmpresaId, ct);
        ViewBag.Municipios = await _lookups.GetCatalogoAsync(NeoSTP.Domain.Common.CatalogCodes.MunicipioEs, _currentUser.EmpresaId, null, ct);
    }

    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] string? search, [FromQuery] int page = 1, CancellationToken ct = default)
    {
        if (!Has("Core.Empresa.Ver")) return Forbid();
        var result = await _empresas.GetListAsync(_currentUser.EmpresaId, new PagedQuery { Search = search, Page = page, PageSize = 20 }, ct);
        ViewBag.Search = search;
        ViewBag.IsSuperAdmin = _currentUser.TipoUsuarioCodigo == "SUPERADMIN";
        return View(result.Value);
    }

    [HttpGet]
    public async Task<IActionResult> Create(CancellationToken ct)
    {
        if (!Has("SuperAdmin.Empresas.Administrar")) return Forbid();
        await CargarTerritorialAsync(ct);
        return View(new CreateEmpresaViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateEmpresaViewModel model, CancellationToken ct)
    {
        if (!Has("SuperAdmin.Empresas.Administrar")) return Forbid();
        if (!ModelState.IsValid) { await CargarTerritorialAsync(ct); return View(model); }

        var result = await _empresas.CreateAsync(new CreateEmpresaRequest
        {
            Nit = model.Nit, Nrc = model.Nrc,
            RazonSocial = model.RazonSocial, NombreComercial = model.NombreComercial,
            CodigoActividad = model.CodigoActividad, ActividadEconomica = model.ActividadEconomica,
            Departamento = model.Departamento, Municipio = model.Municipio,
            Direccion = model.Direccion, Telefono = model.Telefono, Correo = model.Correo,
        }, _currentUser.Username, ct);

        if (result.IsFailure)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Error.");
            foreach (var e in result.ValidationErrors) ModelState.AddModelError(string.Empty, e);
            await CargarTerritorialAsync(ct);
            return View(model);
        }

        TempData["Success"] = $"Empresa {result.Value!.RazonSocial} creada.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id, CancellationToken ct)
    {
        if (!Has("Core.Empresa.Editar")) return Forbid();
        var result = await _empresas.GetByIdAsync(_currentUser.EmpresaId, id, ct);
        if (result.IsFailure) return NotFound();
        var e = result.Value!;
        await CargarTerritorialAsync(ct);
        return View(new EditEmpresaViewModel
        {
            Id = e.Id, Nit = e.Nit, Nrc = e.Nrc,
            RazonSocial = e.RazonSocial, NombreComercial = e.NombreComercial,
            CodigoActividad = e.CodigoActividad, ActividadEconomica = e.ActividadEconomica,
            Departamento = e.Departamento, Municipio = e.Municipio,
            Direccion = e.Direccion, Telefono = e.Telefono, Correo = e.Correo,
            EstadoCodigo = e.EstadoCodigo,
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(EditEmpresaViewModel model, CancellationToken ct)
    {
        if (!Has("Core.Empresa.Editar")) return Forbid();
        if (!ModelState.IsValid) { await CargarTerritorialAsync(ct); return View(model); }

        var result = await _empresas.UpdateAsync(_currentUser.EmpresaId, model.Id, new UpdateEmpresaRequest
        {
            Nrc = model.Nrc, RazonSocial = model.RazonSocial,
            NombreComercial = model.NombreComercial,
            CodigoActividad = model.CodigoActividad, ActividadEconomica = model.ActividadEconomica,
            Departamento = model.Departamento, Municipio = model.Municipio,
            Direccion = model.Direccion, Telefono = model.Telefono, Correo = model.Correo,
            EstadoCodigo = model.EstadoCodigo,
        }, _currentUser.Username, ct);

        if (result.IsFailure)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Error.");
            await CargarTerritorialAsync(ct);
            return View(model);
        }

        TempData["Success"] = "Empresa actualizada.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Licencia(int id, CancellationToken ct)
    {
        if (!Has("Core.Empresa.Ver")) return Forbid();
        if (_currentUser.EmpresaId is not null && _currentUser.EmpresaId != id) return Forbid();

        var result = await _empresas.GetLicenciaAsync(id, ct);
        if (result.IsFailure) return NotFound();
        return View(result.Value);
    }

    private bool Has(string codigo)
        => _currentUser.TipoUsuarioCodigo == "SUPERADMIN" || _currentUser.HasPermiso(codigo);
}
