using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Catalogos;
using NeoSTP.Application.Clientes;
using NeoSTP.Application.Clientes.Dtos;
using NeoSTP.Application.Common;
using NeoSTP.Web.Models;

namespace NeoSTP.Web.Controllers;

[Authorize]
public class ClientesController : Controller
{
    private readonly IClientesService _clientes;
    private readonly ICatalogosService _catalogos;
    private readonly ICurrentUser _currentUser;
    private readonly NeoSTP.Application.Empresas.IEmpresaContext _empresaContext;

    public ClientesController(
        IClientesService clientes,
        ICatalogosService catalogos,
        ICurrentUser currentUser,
        NeoSTP.Application.Empresas.IEmpresaContext empresaContext)
    {
        _clientes = clientes;
        _catalogos = catalogos;
        _currentUser = currentUser;
        _empresaContext = empresaContext;
    }

    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] string? search, [FromQuery] int page = 1, CancellationToken ct = default)
    {
        if (!Has("Clientes.Ver")) return Forbid();
        if (RequireEmpresa() is not int eid) return RedirectToSoporte();

        var result = await _clientes.GetListAsync(eid, new PagedQuery { Search = search, Page = page, PageSize = 20 }, ct);
        ViewBag.Search = search;
        return View(result.Value);
    }

    [HttpGet]
    public async Task<IActionResult> Create(CancellationToken ct)
    {
        if (!Has("Clientes.Crear")) return Forbid();
        await LoadCatalogosAsync(ct);
        return View(new CreateClienteViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateClienteViewModel model, CancellationToken ct)
    {
        if (!Has("Clientes.Crear")) return Forbid();
        if (RequireEmpresa() is not int eid) return Forbid();

        if (!ModelState.IsValid)
        {
            await LoadCatalogosAsync(ct);
            return View(model);
        }

        var result = await _clientes.CreateAsync(eid, ToCreate(model), _currentUser.Username, ct);
        if (result.IsFailure)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Error.");
            foreach (var e in result.ValidationErrors) ModelState.AddModelError(string.Empty, e);
            await LoadCatalogosAsync(ct);
            return View(model);
        }

        TempData["Success"] = $"Cliente {result.Value!.Nombre} creado.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id, CancellationToken ct)
    {
        if (!Has("Clientes.Editar")) return Forbid();
        if (RequireEmpresa() is not int eid) return Forbid();

        var result = await _clientes.GetByIdAsync(eid, id, ct);
        if (result.IsFailure) return NotFound();
        await LoadCatalogosAsync(ct);

        var c = result.Value!;
        return View(new EditClienteViewModel
        {
            Id = c.Id,
            TipoDocumentoCodigo = c.TipoDocumentoCodigo,
            NumeroDocumento = c.NumeroDocumento,
            Nrc = c.Nrc,
            Nombre = c.Nombre,
            NombreComercial = c.NombreComercial,
            TipoContribuyenteCodigo = c.TipoContribuyenteCodigo,
            CodigoActividad = c.CodigoActividad,
            ActividadEconomica = c.ActividadEconomica,
            DepartamentoCodigo = c.DepartamentoCodigo,
            MunicipioCodigo = c.MunicipioCodigo,
            Direccion = c.Direccion,
            Correo = c.Correo,
            Telefono = c.Telefono,
            EstadoCodigo = c.EstadoCodigo,
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(EditClienteViewModel model, CancellationToken ct)
    {
        if (!Has("Clientes.Editar")) return Forbid();
        if (RequireEmpresa() is not int eid) return Forbid();

        if (!ModelState.IsValid)
        {
            await LoadCatalogosAsync(ct);
            return View(model);
        }

        var update = new UpdateClienteRequest
        {
            TipoDocumentoCodigo = model.TipoDocumentoCodigo,
            NumeroDocumento = model.NumeroDocumento,
            Nrc = model.Nrc,
            Nombre = model.Nombre,
            NombreComercial = model.NombreComercial,
            TipoContribuyenteCodigo = model.TipoContribuyenteCodigo,
            CodigoActividad = model.CodigoActividad,
            ActividadEconomica = model.ActividadEconomica,
            DepartamentoCodigo = model.DepartamentoCodigo,
            MunicipioCodigo = model.MunicipioCodigo,
            Direccion = model.Direccion,
            Correo = model.Correo,
            Telefono = model.Telefono,
            EstadoCodigo = model.EstadoCodigo,
        };

        var result = await _clientes.UpdateAsync(eid, model.Id, update, _currentUser.Username, ct);
        if (result.IsFailure)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Error.");
            foreach (var e in result.ValidationErrors) ModelState.AddModelError(string.Empty, e);
            await LoadCatalogosAsync(ct);
            return View(model);
        }

        TempData["Success"] = "Cliente actualizado.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Inactivar(int id, CancellationToken ct)
    {
        if (!Has("Clientes.Editar")) return Forbid();
        if (RequireEmpresa() is not int eid) return Forbid();

        var result = await _clientes.InactivarAsync(eid, id, _currentUser.Username, ct);
        TempData[result.IsSuccess ? "Success" : "Error"] = result.IsSuccess ? "Cliente inactivado." : result.Error;
        return RedirectToAction(nameof(Index));
    }

    private static CreateClienteRequest ToCreate(CreateClienteViewModel m) => new()
    {
        TipoDocumentoCodigo = m.TipoDocumentoCodigo,
        NumeroDocumento = m.NumeroDocumento,
        Nrc = m.Nrc,
        Nombre = m.Nombre,
        NombreComercial = m.NombreComercial,
        TipoContribuyenteCodigo = m.TipoContribuyenteCodigo,
        CodigoActividad = m.CodigoActividad,
        ActividadEconomica = m.ActividadEconomica,
        DepartamentoCodigo = m.DepartamentoCodigo,
        MunicipioCodigo = m.MunicipioCodigo,
        Direccion = m.Direccion,
        Correo = m.Correo,
        Telefono = m.Telefono,
    };

    private bool Has(string codigo)
        => _currentUser.TipoUsuarioCodigo == "SUPERADMIN" || _currentUser.HasPermiso(codigo);

    private int? RequireEmpresa() => _empresaContext.CurrentEmpresaId;

    private IActionResult RedirectToSoporte()
    {
        if (_currentUser.TipoUsuarioCodigo == "SUPERADMIN")
        {
            TempData["Error"] = "Esta pantalla opera dentro de una empresa. Selecciona una en modo soporte primero.";
            return RedirectToAction("Index", "Soporte");
        }
        return RedirectToAction("Index", "Home");
    }

    private async Task LoadCatalogosAsync(CancellationToken ct)
    {
        var empresaId = _currentUser.EmpresaId;
        async Task<IReadOnlyList<NeoSTP.Application.Catalogos.Dtos.CatalogoItemDto>> Items(string code)
            => (await _catalogos.GetItemsAsync(code, empresaId, ct)).Value
               ?? new List<NeoSTP.Application.Catalogos.Dtos.CatalogoItemDto>();

        ViewBag.TiposDoc = await Items("TIPO_DOC_IDENTIDAD");
        ViewBag.TiposContrib = await Items("TIPO_CONTRIBUYENTE");
        ViewBag.Departamentos = await Items("DEPARTAMENTO_ES");
        ViewBag.Estados = await Items("ESTADO_GENERICO");
    }
}
