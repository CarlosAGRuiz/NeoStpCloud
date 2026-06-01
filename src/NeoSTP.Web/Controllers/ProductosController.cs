using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Catalogos;
using NeoSTP.Application.Common;
using NeoSTP.Application.Productos;
using NeoSTP.Application.Productos.Dtos;
using NeoSTP.Web.Models;

namespace NeoSTP.Web.Controllers;

[Authorize]
public class ProductosController : Controller
{
    private readonly IProductosService _productos;
    private readonly ICatalogosService _catalogos;
    private readonly ICurrentUser _currentUser;
    private readonly NeoSTP.Application.Empresas.IEmpresaContext _empresaContext;

    public ProductosController(
        IProductosService productos,
        ICatalogosService catalogos,
        ICurrentUser currentUser,
        NeoSTP.Application.Empresas.IEmpresaContext empresaContext)
    {
        _productos = productos;
        _catalogos = catalogos;
        _currentUser = currentUser;
        _empresaContext = empresaContext;
    }

    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] string? search, [FromQuery] int page = 1, CancellationToken ct = default)
    {
        if (!Has("Productos.Ver")) return Forbid();
        if (RequireEmpresa() is not int eid) return RedirectToSoporte();

        var result = await _productos.GetListAsync(eid, new PagedQuery { Search = search, Page = page, PageSize = 20 }, ct);
        ViewBag.Search = search;
        return View(result.Value);
    }

    [HttpGet]
    public async Task<IActionResult> Create(CancellationToken ct)
    {
        if (!Has("Productos.Crear")) return Forbid();
        await LoadCatalogosAsync(ct);
        return View(new CreateProductoViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateProductoViewModel model, CancellationToken ct)
    {
        if (!Has("Productos.Crear")) return Forbid();
        if (RequireEmpresa() is not int eid) return Forbid();
        if (!ModelState.IsValid)
        {
            await LoadCatalogosAsync(ct);
            return View(model);
        }

        var result = await _productos.CreateAsync(eid, ToCreate(model), _currentUser.Username, ct);
        if (result.IsFailure)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Error.");
            foreach (var e in result.ValidationErrors) ModelState.AddModelError(string.Empty, e);
            await LoadCatalogosAsync(ct);
            return View(model);
        }

        TempData["Success"] = $"Producto {result.Value!.Nombre} creado.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id, CancellationToken ct)
    {
        if (!Has("Productos.Editar")) return Forbid();
        if (RequireEmpresa() is not int eid) return Forbid();
        var result = await _productos.GetByIdAsync(eid, id, ct);
        if (result.IsFailure) return NotFound();
        await LoadCatalogosAsync(ct);

        var p = result.Value!;
        return View(new EditProductoViewModel
        {
            Id = p.Id,
            CodigoInterno = p.CodigoInterno,
            CodigoBarra = p.CodigoBarra,
            Nombre = p.Nombre,
            Descripcion = p.Descripcion,
            TipoItem = p.TipoItem,
            UnidadMedidaCodigo = p.UnidadMedidaCodigo,
            PrecioUnitario = p.PrecioUnitario,
            CostoUnitario = p.CostoUnitario,
            AplicaIva = p.AplicaIva,
            TributoCodigo = p.TributoCodigo,
            EstadoCodigo = p.EstadoCodigo,
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(EditProductoViewModel model, CancellationToken ct)
    {
        if (!Has("Productos.Editar")) return Forbid();
        if (RequireEmpresa() is not int eid) return Forbid();
        if (!ModelState.IsValid)
        {
            await LoadCatalogosAsync(ct);
            return View(model);
        }

        var update = new UpdateProductoRequest
        {
            CodigoInterno = model.CodigoInterno,
            CodigoBarra = model.CodigoBarra,
            Nombre = model.Nombre,
            Descripcion = model.Descripcion,
            TipoItem = model.TipoItem,
            UnidadMedidaCodigo = model.UnidadMedidaCodigo,
            PrecioUnitario = model.PrecioUnitario,
            CostoUnitario = model.CostoUnitario,
            AplicaIva = model.AplicaIva,
            TributoCodigo = model.TributoCodigo,
            EstadoCodigo = model.EstadoCodigo,
        };

        var result = await _productos.UpdateAsync(eid, model.Id, update, _currentUser.Username, ct);
        if (result.IsFailure)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Error.");
            foreach (var e in result.ValidationErrors) ModelState.AddModelError(string.Empty, e);
            await LoadCatalogosAsync(ct);
            return View(model);
        }

        TempData["Success"] = "Producto actualizado.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Inactivar(int id, CancellationToken ct)
    {
        if (!Has("Productos.Editar")) return Forbid();
        if (RequireEmpresa() is not int eid) return Forbid();
        var result = await _productos.InactivarAsync(eid, id, _currentUser.Username, ct);
        TempData[result.IsSuccess ? "Success" : "Error"] = result.IsSuccess ? "Producto inactivado." : result.Error;
        return RedirectToAction(nameof(Index));
    }

    private static CreateProductoRequest ToCreate(CreateProductoViewModel m) => new()
    {
        CodigoInterno = m.CodigoInterno,
        CodigoBarra = m.CodigoBarra,
        Nombre = m.Nombre,
        Descripcion = m.Descripcion,
        TipoItem = m.TipoItem,
        UnidadMedidaCodigo = m.UnidadMedidaCodigo,
        PrecioUnitario = m.PrecioUnitario,
        CostoUnitario = m.CostoUnitario,
        AplicaIva = m.AplicaIva,
        TributoCodigo = m.TributoCodigo,
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
            => (await _catalogos.GetItemsAsync(code, empresaId, ct: ct)).Value
               ?? new List<NeoSTP.Application.Catalogos.Dtos.CatalogoItemDto>();

        ViewBag.UnidadesMedida = await Items("UNIDAD_MEDIDA");
        ViewBag.Estados = await Items("ESTADO_GENERICO");
    }
}
