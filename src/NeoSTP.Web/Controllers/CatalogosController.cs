using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Catalogos;
using NeoSTP.Application.Catalogos.Dtos;
using NeoSTP.Application.Empresas;
using NeoSTP.Web.Models;

namespace NeoSTP.Web.Controllers;

[Authorize]
public class CatalogosController : Controller
{
    private readonly ICatalogosService _catalogos;
    private readonly ICurrentUser _currentUser;
    private readonly IEmpresaContext _empresaContext;

    public CatalogosController(ICatalogosService catalogos, ICurrentUser currentUser, IEmpresaContext empresaContext)
    {
        _catalogos = catalogos;
        _currentUser = currentUser;
        _empresaContext = empresaContext;
    }

    // ----- Lista -----

    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] string? search, CancellationToken ct)
    {
        if (!Has("Core.Catalogos.Ver")) return Forbid();

        var empresaId = _empresaContext.CurrentEmpresaId;
        var result = await _catalogos.GetListAsync(empresaId, ct);
        var items = result.Value ?? Array.Empty<CatalogoDto>();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            items = items
                .Where(c =>
                    c.Codigo.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    c.Nombre.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    (!string.IsNullOrWhiteSpace(c.Descripcion) && c.Descripcion.Contains(term, StringComparison.OrdinalIgnoreCase)))
                .ToArray();
        }

        ViewBag.EmpresaId = empresaId;
        ViewBag.Search = search;
        ViewBag.PuedeAdministrar = Has("Core.Catalogos.Administrar");
        ViewBag.PuedeImportar = Has("Core.Catalogos.Importar");
        return View(items);
    }

    // ----- Detalle (ítems) -----

    [HttpGet("Catalogos/Details/{codigo}")]
    public async Task<IActionResult> Details(string codigo, [FromQuery] string? parent, CancellationToken ct)
    {
        if (!Has("Core.Catalogos.Ver")) return Forbid();

        var empresaId = _empresaContext.CurrentEmpresaId;
        var cat = await _catalogos.GetByCodigoAsync(codigo, empresaId, ct);
        if (cat.IsFailure) return NotFound();

        var items = await _catalogos.GetItemsAsync(codigo, empresaId, parent, ct);

        ViewBag.Catalogo = cat.Value!;
        ViewBag.ParentFilter = parent;
        ViewBag.PuedeAdministrar = Has("Core.Catalogos.Administrar");
        ViewBag.PuedeImportar = Has("Core.Catalogos.Importar");
        // Editable solo si el catálogo pertenece al ámbito actual (empresa propia,
        // o global cuando se opera como SuperAdmin sin empresa).
        ViewBag.EsEditable = cat.Value!.EmpresaId == empresaId;
        return View(items.Value ?? Array.Empty<CatalogoItemDto>());
    }

    // ----- Crear catálogo propio -----

    [HttpGet]
    public IActionResult Create()
    {
        if (!Has("Core.Catalogos.Administrar")) return Forbid();
        return View(new CreateCatalogoViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateCatalogoViewModel model, CancellationToken ct)
    {
        if (!Has("Core.Catalogos.Administrar")) return Forbid();
        if (!ModelState.IsValid) return View(model);

        var result = await _catalogos.CreateAsync(_empresaContext.CurrentEmpresaId, new CreateCatalogoRequest
        {
            Codigo = model.Codigo,
            Nombre = model.Nombre,
            Descripcion = model.Descripcion,
        }, _currentUser.Username, ct);

        if (result.IsFailure)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Error al crear el catálogo.");
            return View(model);
        }

        TempData["Success"] = $"Catálogo {result.Value!.Codigo} creado.";
        return RedirectToAction(nameof(Details), new { codigo = result.Value.Codigo });
    }

    // ----- Editar catálogo -----

    [HttpGet("Catalogos/Edit/{codigo}")]
    public async Task<IActionResult> Edit(string codigo, CancellationToken ct)
    {
        if (!Has("Core.Catalogos.Administrar")) return Forbid();

        var empresaId = _empresaContext.CurrentEmpresaId;
        var cat = await _catalogos.GetByCodigoAsync(codigo, empresaId, ct);
        if (cat.IsFailure || cat.Value!.EmpresaId != empresaId) return NotFound();

        return View(new EditCatalogoViewModel
        {
            Codigo = cat.Value.Codigo,
            Nombre = cat.Value.Nombre,
            Descripcion = cat.Value.Descripcion,
            Activo = cat.Value.Activo,
        });
    }

    [HttpPost("Catalogos/Edit/{codigo}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(string codigo, EditCatalogoViewModel model, CancellationToken ct)
    {
        if (!Has("Core.Catalogos.Administrar")) return Forbid();
        if (!ModelState.IsValid) { model.Codigo = codigo; return View(model); }

        var result = await _catalogos.UpdateAsync(_empresaContext.CurrentEmpresaId, codigo, new UpdateCatalogoRequest
        {
            Nombre = model.Nombre,
            Descripcion = model.Descripcion,
            Activo = model.Activo,
        }, _currentUser.Username, ct);

        if (result.IsFailure)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Error al actualizar.");
            model.Codigo = codigo;
            return View(model);
        }

        TempData["Success"] = $"Catálogo {codigo.ToUpperInvariant()} actualizado.";
        return RedirectToAction(nameof(Details), new { codigo });
    }

    // ----- Ítems -----

    [HttpPost("Catalogos/CreateItem/{codigo}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateItem(string codigo, CatalogoItemFormViewModel model, CancellationToken ct)
    {
        if (!Has("Core.Catalogos.Administrar")) return Forbid();

        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Código y valor del ítem son obligatorios.";
            return RedirectToAction(nameof(Details), new { codigo });
        }

        var result = await _catalogos.CreateItemAsync(_empresaContext.CurrentEmpresaId, codigo, new CreateCatalogoItemRequest
        {
            Codigo = model.Codigo,
            Valor = model.Valor,
            Descripcion = model.Descripcion,
            Orden = model.Orden,
            ParentCodigo = model.ParentCodigo,
        }, _currentUser.Username, ct);

        TempData[result.IsSuccess ? "Success" : "Error"] = result.IsSuccess
            ? $"Ítem {result.Value!.Codigo} agregado."
            : result.Error;
        return RedirectToAction(nameof(Details), new { codigo });
    }

    [HttpGet("Catalogos/EditItem/{codigo}/{itemId:int}")]
    public async Task<IActionResult> EditItem(string codigo, int itemId, CancellationToken ct)
    {
        if (!Has("Core.Catalogos.Administrar")) return Forbid();

        var empresaId = _empresaContext.CurrentEmpresaId;
        var cat = await _catalogos.GetByCodigoAsync(codigo, empresaId, ct);
        if (cat.IsFailure || cat.Value!.EmpresaId != empresaId) return NotFound();

        var items = await _catalogos.GetItemsAsync(codigo, empresaId, ct: ct);
        var item = items.Value?.FirstOrDefault(i => i.Id == itemId);
        if (item is null) return NotFound();

        ViewBag.Catalogo = cat.Value;
        return View(new CatalogoItemFormViewModel
        {
            ItemId = item.Id,
            Codigo = item.Codigo,
            Valor = item.Valor,
            Descripcion = item.Descripcion,
            Orden = item.Orden,
            ParentCodigo = item.ParentCodigo,
            Activo = item.Activo,
        });
    }

    [HttpPost("Catalogos/EditItem/{codigo}/{itemId:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditItem(string codigo, int itemId, CatalogoItemFormViewModel model, CancellationToken ct)
    {
        if (!Has("Core.Catalogos.Administrar")) return Forbid();

        var empresaId = _empresaContext.CurrentEmpresaId;
        if (!ModelState.IsValid)
        {
            var cat = await _catalogos.GetByCodigoAsync(codigo, empresaId, ct);
            if (cat.IsFailure) return NotFound();
            ViewBag.Catalogo = cat.Value!;
            model.ItemId = itemId;
            return View(model);
        }

        var result = await _catalogos.UpdateItemAsync(empresaId, codigo, itemId, new UpdateCatalogoItemRequest
        {
            Valor = model.Valor,
            Descripcion = model.Descripcion,
            Orden = model.Orden,
            Activo = model.Activo,
            ParentCodigo = model.ParentCodigo ?? string.Empty,
        }, _currentUser.Username, ct);

        if (result.IsFailure)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Error al actualizar el ítem.");
            var cat = await _catalogos.GetByCodigoAsync(codigo, empresaId, ct);
            if (cat.IsSuccess) ViewBag.Catalogo = cat.Value!;
            model.ItemId = itemId;
            return View(model);
        }

        TempData["Success"] = $"Ítem {result.Value!.Codigo} actualizado.";
        return RedirectToAction(nameof(Details), new { codigo });
    }

    [HttpPost("Catalogos/DeleteItem/{codigo}/{itemId:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteItem(string codigo, int itemId, CancellationToken ct)
    {
        if (!Has("Core.Catalogos.Administrar")) return Forbid();

        var result = await _catalogos.DeleteItemAsync(_empresaContext.CurrentEmpresaId, codigo, itemId, _currentUser.Username, ct);
        TempData[result.IsSuccess ? "Success" : "Error"] = result.IsSuccess ? "Ítem eliminado." : result.Error;
        return RedirectToAction(nameof(Details), new { codigo });
    }

    // ----- Importar -----

    [HttpGet("Catalogos/Import/{codigo}")]
    public async Task<IActionResult> Import(string codigo, CancellationToken ct)
    {
        if (!Has("Core.Catalogos.Importar")) return Forbid();

        var cat = await _catalogos.GetByCodigoAsync(codigo, _empresaContext.CurrentEmpresaId, ct);
        if (cat.IsFailure) return NotFound();

        ViewBag.Catalogo = cat.Value!;
        return View();
    }

    [HttpPost("Catalogos/Import/{codigo}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Import(
        string codigo,
        IFormFile? file,
        bool dryRun = false,
        CatalogoImportMode mode = CatalogoImportMode.Upsert,
        CancellationToken ct = default)
    {
        if (!Has("Core.Catalogos.Importar")) return Forbid();

        var empresaId = _empresaContext.CurrentEmpresaId;
        var cat = await _catalogos.GetByCodigoAsync(codigo, empresaId, ct);
        if (cat.IsFailure) return NotFound();
        ViewBag.Catalogo = cat.Value!;

        if (file is null || file.Length == 0)
        {
            ModelState.AddModelError(string.Empty, "Debe seleccionar un archivo.");
            return View();
        }

        var format = DetectFormat(file);

        await using var stream = file.OpenReadStream();
        var result = await _catalogos.ImportItemsAsync(empresaId, codigo,
            new CatalogoImportRequest { Format = format, Content = stream, DryRun = dryRun, Mode = mode },
            _currentUser.Username, ct);

        if (result.IsFailure)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Error al importar.");
            return View();
        }

        ViewBag.ImportResult = result.Value!;
        ViewBag.FileName = file.FileName;
        if (!dryRun)
        {
            TempData["Success"] = $"Importación aplicada: {result.Value!.Inserted} insertados, {result.Value.Updated} actualizados, {result.Value.Skipped} omitidos, {result.Value.ErrorCount} errores.";
        }
        return View();
    }

    // ----- Exportar -----

    [HttpGet("Catalogos/Export/{codigo}")]
    public async Task<IActionResult> Export(string codigo, [FromQuery] CatalogoFileFormat format = CatalogoFileFormat.Csv, CancellationToken ct = default)
    {
        if (!Has("Core.Catalogos.Ver")) return Forbid();

        var empresaId = _empresaContext.CurrentEmpresaId;
        var result = await _catalogos.ExportItemsAsync(empresaId, codigo, format, ct);
        if (result.IsFailure) return NotFound();

        var f = result.Value!;
        return File(f.Content, f.ContentType, f.FileName);
    }

    // ----- Helpers -----

    private bool Has(string codigo)
        => _currentUser.TipoUsuarioCodigo == "SUPERADMIN" || _currentUser.HasPermiso(codigo);

    private static CatalogoFileFormat DetectFormat(IFormFile file)
    {
        var name = (file.FileName ?? string.Empty).ToLowerInvariant();
        if (name.EndsWith(".json")) return CatalogoFileFormat.Json;
        if (name.EndsWith(".xlsx") || name.EndsWith(".xlsm")) return CatalogoFileFormat.Xlsx;
        var ct = (file.ContentType ?? string.Empty).ToLowerInvariant();
        if (ct.Contains("json")) return CatalogoFileFormat.Json;
        if (ct.Contains("spreadsheet") || ct.Contains("excel")) return CatalogoFileFormat.Xlsx;
        return CatalogoFileFormat.Csv;
    }
}
