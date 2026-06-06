using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Empresas;
using NeoSTP.Application.Scan;
using NeoSTP.Application.Scan.Dtos;

namespace NeoSTP.Web.Controllers;

/// <summary>
/// DTE recibidos de proveedores (registro/respaldo). Listado/detalle de los documentos
/// generados al confirmar escaneos como "DTE recibido" en NeoScanAI. Solo lectura.
/// Opera dentro de una empresa; SuperAdmin debe seleccionar empresa en modo soporte.
/// Permiso: ScanAI.Ver (mismo origen que NeoScan).
/// </summary>
[Authorize]
[Route("[controller]")]
public class DteRecibidosController : Controller
{
    private readonly IDteRecibidoService _recibidos;
    private readonly ICurrentUser _currentUser;
    private readonly IEmpresaContext _empresaContext;

    public DteRecibidosController(IDteRecibidoService recibidos, ICurrentUser currentUser, IEmpresaContext empresaContext)
    {
        _recibidos = recibidos;
        _currentUser = currentUser;
        _empresaContext = empresaContext;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(string? search, DateOnly? desde, DateOnly? hasta, int page = 1, CancellationToken ct = default)
    {
        if (!Has("ScanAI.Ver")) return Forbid();
        if (RequireEmpresa() is not int eid) return RedirectToSoporte();

        var query = new DteRecibidoQuery
        {
            Search = search,
            Desde = desde,
            Hasta = hasta,
            Page = page <= 0 ? 1 : page,
            PageSize = 20,
        };
        var result = await _recibidos.ListAsync(eid, query, ct);
        if (result.IsFailure) TempData["Error"] = result.Error;

        return View(new DteRecibidosIndexViewModel
        {
            Recibidos = result.Value ?? Application.Common.PagedResult<DteRecibidoDto>.Create(Array.Empty<DteRecibidoDto>(), 0, query.Page, query.PageSize),
            Search = search,
            Desde = desde,
            Hasta = hasta,
            Page = query.Page,
        });
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Detalle(int id, CancellationToken ct)
    {
        if (!Has("ScanAI.Ver")) return Forbid();
        if (RequireEmpresa() is not int eid) return RedirectToSoporte();

        var result = await _recibidos.GetAsync(eid, id, ct);
        if (result.IsFailure) return NotFound();
        return View(result.Value);
    }

    private bool Has(string codigo)
        => _currentUser.TipoUsuarioCodigo == "SUPERADMIN" || _currentUser.HasPermiso(codigo);

    private int? RequireEmpresa() => _empresaContext.CurrentEmpresaId;

    private IActionResult RedirectToSoporte()
    {
        if (_currentUser.TipoUsuarioCodigo == "SUPERADMIN")
        {
            TempData["Error"] = "Los DTE recibidos operan dentro de una empresa. Selecciona una en modo soporte primero.";
            return RedirectToAction("Index", "Soporte");
        }
        return RedirectToAction("Index", "Home");
    }
}

public class DteRecibidosIndexViewModel
{
    public Application.Common.PagedResult<DteRecibidoDto> Recibidos { get; set; }
        = Application.Common.PagedResult<DteRecibidoDto>.Create(Array.Empty<DteRecibidoDto>(), 0, 1, 20);
    public string? Search { get; set; }
    public DateOnly? Desde { get; set; }
    public DateOnly? Hasta { get; set; }
    public int Page { get; set; } = 1;
}
