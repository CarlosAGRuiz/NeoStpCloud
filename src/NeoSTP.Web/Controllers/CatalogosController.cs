using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Catalogos;
using NeoSTP.Application.Catalogos.Dtos;
using NeoSTP.Application.Empresas;

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
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        if (!Has("Core.Catalogos.Ver")) return Forbid();

        var empresaId = _empresaContext.CurrentEmpresaId;
        var result = await _catalogos.GetListAsync(empresaId, ct);
        ViewBag.EmpresaId = empresaId;
        ViewBag.PuedeAdministrar = Has("Core.Catalogos.Administrar");
        ViewBag.PuedeImportar = Has("Core.Catalogos.Importar");
        return View(result.Value ?? Array.Empty<CatalogoDto>());
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
        return View(items.Value ?? Array.Empty<CatalogoItemDto>());
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
