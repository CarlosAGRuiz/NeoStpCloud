using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NeoSTP.Api.Authorization;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Catalogos;
using NeoSTP.Application.Catalogos.Dtos;

namespace NeoSTP.Api.Controllers;

[Authorize]
[Route("api/catalogos")]
public class CatalogosController : ApiControllerBase
{
    private readonly ICatalogosService _service;
    private readonly ICurrentUser _currentUser;

    public CatalogosController(ICatalogosService service, ICurrentUser currentUser)
    {
        _service = service;
        _currentUser = currentUser;
    }

    // ------------------------------------------------------------ Lectura

    [HttpGet]
    [RequirePermiso("Core.Catalogos.Ver")]
    public async Task<IActionResult> List([FromQuery] int? empresaId, CancellationToken ct)
        => Respond(await _service.GetListAsync(ResolveEmpresaId(empresaId), ct));

    [HttpGet("{codigo}")]
    [RequirePermiso("Core.Catalogos.Ver")]
    public async Task<IActionResult> Get(string codigo, [FromQuery] int? empresaId, CancellationToken ct)
        => Respond(await _service.GetByCodigoAsync(codigo, ResolveEmpresaId(empresaId), ct));

    [HttpGet("{codigo}/items")]
    [RequirePermiso("Core.Catalogos.Ver")]
    public async Task<IActionResult> Items(string codigo, [FromQuery] string? parent, [FromQuery] int? empresaId, CancellationToken ct)
        => Respond(await _service.GetItemsAsync(codigo, ResolveEmpresaId(empresaId), parent, ct));

    // -------------------------------------------------------- CRUD catálogos

    [HttpPost]
    [RequirePermiso("Core.Catalogos.Administrar")]
    public async Task<IActionResult> Create([FromBody] CreateCatalogoRequest request, [FromQuery] int? empresaId, CancellationToken ct)
        => Respond(await _service.CreateAsync(ResolveEmpresaId(empresaId), request, _currentUser.Username, ct));

    [HttpPut("{codigo}")]
    [RequirePermiso("Core.Catalogos.Administrar")]
    public async Task<IActionResult> Update(string codigo, [FromBody] UpdateCatalogoRequest request, [FromQuery] int? empresaId, CancellationToken ct)
        => Respond(await _service.UpdateAsync(ResolveEmpresaId(empresaId), codigo, request, _currentUser.Username, ct));

    // ------------------------------------------------------------ CRUD ítems

    [HttpPost("{codigo}/items")]
    [RequirePermiso("Core.Catalogos.Administrar")]
    public async Task<IActionResult> CreateItem(string codigo, [FromBody] CreateCatalogoItemRequest request, [FromQuery] int? empresaId, CancellationToken ct)
        => Respond(await _service.CreateItemAsync(ResolveEmpresaId(empresaId), codigo, request, _currentUser.Username, ct));

    [HttpPut("{codigo}/items/{id:int}")]
    [RequirePermiso("Core.Catalogos.Administrar")]
    public async Task<IActionResult> UpdateItem(string codigo, int id, [FromBody] UpdateCatalogoItemRequest request, [FromQuery] int? empresaId, CancellationToken ct)
        => Respond(await _service.UpdateItemAsync(ResolveEmpresaId(empresaId), codigo, id, request, _currentUser.Username, ct));

    [HttpDelete("{codigo}/items/{id:int}")]
    [RequirePermiso("Core.Catalogos.Administrar")]
    public async Task<IActionResult> DeleteItem(string codigo, int id, [FromQuery] int? empresaId, CancellationToken ct)
        => Respond(await _service.DeleteItemAsync(ResolveEmpresaId(empresaId), codigo, id, _currentUser.Username, ct), "Ítem eliminado.");

    // ----------------------------------------------------- Import / Export

    [HttpPost("{codigo}/import")]
    [RequirePermiso("Core.Catalogos.Importar")]
    [RequestSizeLimit(10_000_000)] // 10 MB
    public async Task<IActionResult> Import(
        string codigo,
        IFormFile file,
        [FromQuery] CatalogoFileFormat? format,
        [FromQuery] bool dryRun = false,
        [FromQuery] CatalogoImportMode mode = CatalogoImportMode.Upsert,
        [FromQuery] int? empresaId = null,
        CancellationToken ct = default)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest(Shared.ApiResponse.Fail("Archivo requerido.", new[] { "VALIDATION" }, HttpContext.TraceIdentifier));
        }

        var resolvedFormat = format ?? DetectFormat(file);
        await using var stream = file.OpenReadStream();
        return Respond(await _service.ImportItemsAsync(
            ResolveEmpresaId(empresaId), codigo,
            new CatalogoImportRequest
            {
                Format = resolvedFormat,
                Content = stream,
                DryRun = dryRun,
                Mode = mode,
            },
            _currentUser.Username, ct));
    }

    [HttpGet("{codigo}/export")]
    [RequirePermiso("Core.Catalogos.Ver")]
    public async Task<IActionResult> Export(
        string codigo,
        [FromQuery] CatalogoFileFormat format = CatalogoFileFormat.Csv,
        [FromQuery] int? empresaId = null,
        CancellationToken ct = default)
    {
        var result = await _service.ExportItemsAsync(ResolveEmpresaId(empresaId), codigo, format, ct);
        if (result.IsFailure) return Respond(result);
        var f = result.Value!;
        return File(f.Content, f.ContentType, f.FileName);
    }

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

    // El ResolveEmpresaId acepta null como ámbito 'sistema' válido para SuperAdmin
    // (a diferencia de SucursalesController que siempre exige un tenant).
    private int? ResolveEmpresaId(int? fromRequest) => _currentUser.EmpresaId ?? fromRequest;
}
