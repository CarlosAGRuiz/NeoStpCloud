using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NeoSTP.Web.Auth;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Empresas;
using NeoSTP.Application.Profit.Dtos;
using NeoSTP.Application.Scan;
using NeoSTP.Application.Scan.Dtos;

namespace NeoSTP.Web.Controllers;

/// <summary>
/// NeoScanAI (B-3): bandeja web de documentos capturados, revisión/corrección de campos
/// y conversión a gasto, compra o DTE recibido. UI sobre los servicios expuestos a NeoCloud Mobile.
/// Opera dentro de una empresa; SuperAdmin debe seleccionar empresa en modo soporte.
/// Lectura/corrección: ScanAI.Ver; confirmaciones/rechazo: ScanAI.Confirmar.
/// </summary>
[Authorize]
[RequireModulo("NEOSCANAI")]
[Route("[controller]")]
public class ScanController : Controller
{
    /// <summary>Estados visibles en el filtro de la bandeja.</summary>
    public static readonly string[] Estados =
        ["PROCESANDO", "PROCESADO", "REQUIERE_REVISION", "CONFIRMADO", "RECHAZADO", "ERROR"];

    /// <summary>Categorías de gasto (mismas que NeoProfit).</summary>
    public static readonly string[] CategoriasGasto = ProfitGastosController.Categorias;

    private readonly IScanService _scan;
    private readonly ICurrentUser _currentUser;
    private readonly IEmpresaContext _empresaContext;

    public ScanController(IScanService scan, ICurrentUser currentUser, IEmpresaContext empresaContext)
    {
        _scan = scan;
        _currentUser = currentUser;
        _empresaContext = empresaContext;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(string? search, string? estado, int page = 1, CancellationToken ct = default)
    {
        if (!Has("ScanAI.Ver")) return Forbid();
        if (RequireEmpresa() is not int eid) return RedirectToSoporte();

        var query = new ScanQuery
        {
            Search = search,
            EstadoCodigo = string.IsNullOrWhiteSpace(estado) ? null : estado,
            Page = page <= 0 ? 1 : page,
            PageSize = 20,
        };
        var result = await _scan.ListAsync(eid, query, ct);
        if (result.IsFailure) TempData["Error"] = result.Error;

        return View(new ScanIndexViewModel
        {
            Documentos = result.Value ?? Application.Common.PagedResult<ScanDocumentoDto>.Create(Array.Empty<ScanDocumentoDto>(), 0, query.Page, query.PageSize),
            Search = search,
            Estado = estado,
            Page = query.Page,
            Estados = Estados,
            PuedeConfirmar = Has("ScanAI.Confirmar"),
        });
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Detalle(int id, CancellationToken ct)
    {
        if (!Has("ScanAI.Ver")) return Forbid();
        if (RequireEmpresa() is not int eid) return RedirectToSoporte();

        var result = await _scan.GetAsync(eid, id, ct);
        if (result.IsFailure) return NotFound();

        return View(new ScanDetalleViewModel
        {
            Doc = result.Value!,
            PuedeConfirmar = Has("ScanAI.Confirmar"),
            CategoriasGasto = CategoriasGasto,
        });
    }

    [HttpGet("{id:int}/archivo")]
    public async Task<IActionResult> Archivo(int id, CancellationToken ct)
    {
        if (!Has("ScanAI.Ver")) return Forbid();
        if (RequireEmpresa() is not int eid) return NotFound();

        var a = await _scan.GetArchivoAsync(eid, id, ct);
        return a is null ? NotFound() : File(a.Contenido, a.ContentType, a.Nombre);
    }

    [HttpPost("{id:int}/Corregir")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Corregir(int id, CorregirScanRequest request, CancellationToken ct)
    {
        if (!Has("ScanAI.Ver")) return Forbid();
        if (RequireEmpresa() is not int eid) return RedirectToSoporte();

        var result = await _scan.CorregirAsync(eid, id, request, _currentUser.Username, ct);
        TempData[result.IsSuccess ? "Success" : "Error"] = result.IsSuccess ? "Campos actualizados." : result.Error;
        return RedirectToAction(nameof(Detalle), new { id });
    }

    [HttpPost("{id:int}/RegistrarGasto")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RegistrarGasto(int id, CreateProfitGastoRequest request, CancellationToken ct)
    {
        if (!Has("ScanAI.Confirmar")) return Forbid();
        if (RequireEmpresa() is not int eid) return RedirectToSoporte();

        var result = await _scan.ConfirmarComoGastoAsync(eid, id, request, _currentUser.Username, ct);
        TempData[result.IsSuccess ? "Success" : "Error"] = result.IsSuccess ? "Escaneo confirmado como gasto." : result.Error;
        return RedirectToAction(nameof(Detalle), new { id });
    }

    [HttpPost("{id:int}/RegistrarCompra")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RegistrarCompra(int id, CreateProfitCompraRequest request, CancellationToken ct)
    {
        if (!Has("ScanAI.Confirmar")) return Forbid();
        if (RequireEmpresa() is not int eid) return RedirectToSoporte();

        var result = await _scan.ConfirmarComoCompraAsync(eid, id, request, _currentUser.Username, ct);
        TempData[result.IsSuccess ? "Success" : "Error"] = result.IsSuccess ? "Escaneo confirmado como compra." : result.Error;
        return RedirectToAction(nameof(Detalle), new { id });
    }

    [HttpPost("{id:int}/RegistrarDteRecibido")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RegistrarDteRecibido(int id, RegistrarDteRecibidoRequest request, CancellationToken ct)
    {
        if (!Has("ScanAI.Confirmar")) return Forbid();
        if (RequireEmpresa() is not int eid) return RedirectToSoporte();

        var result = await _scan.RegistrarDteRecibidoAsync(eid, id, request, _currentUser.Username, ct);
        TempData[result.IsSuccess ? "Success" : "Error"] = result.IsSuccess ? "Escaneo registrado como DTE recibido." : result.Error;
        return RedirectToAction(nameof(Detalle), new { id });
    }

    [HttpPost("{id:int}/Rechazar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Rechazar(int id, string? motivo, CancellationToken ct)
    {
        if (!Has("ScanAI.Confirmar")) return Forbid();
        if (RequireEmpresa() is not int eid) return RedirectToSoporte();

        var result = await _scan.RechazarAsync(eid, id, motivo, _currentUser.Username, ct);
        TempData[result.IsSuccess ? "Success" : "Error"] = result.IsSuccess ? "Escaneo rechazado." : result.Error;
        return RedirectToAction(nameof(Detalle), new { id });
    }

    private bool Has(string codigo)
        => _currentUser.TipoUsuarioCodigo == "SUPERADMIN" || _currentUser.HasPermiso(codigo);

    private int? RequireEmpresa() => _empresaContext.CurrentEmpresaId;

    private IActionResult RedirectToSoporte()
    {
        if (_currentUser.TipoUsuarioCodigo == "SUPERADMIN")
        {
            TempData["Error"] = "NeoScan opera dentro de una empresa. Selecciona una en modo soporte primero.";
            return RedirectToAction("Index", "Soporte");
        }

        return RedirectToAction("Index", "Home");
    }
}

public class ScanIndexViewModel
{
    public Application.Common.PagedResult<ScanDocumentoDto> Documentos { get; set; }
        = Application.Common.PagedResult<ScanDocumentoDto>.Create(Array.Empty<ScanDocumentoDto>(), 0, 1, 20);
    public string? Search { get; set; }
    public string? Estado { get; set; }
    public int Page { get; set; } = 1;
    public string[] Estados { get; set; } = Array.Empty<string>();
    public bool PuedeConfirmar { get; set; }
}

public class ScanDetalleViewModel
{
    public ScanDocumentoDto Doc { get; set; } = new();
    public bool PuedeConfirmar { get; set; }
    public string[] CategoriasGasto { get; set; } = Array.Empty<string>();
}
