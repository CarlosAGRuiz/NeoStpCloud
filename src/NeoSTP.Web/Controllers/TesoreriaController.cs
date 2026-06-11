using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Common;
using NeoSTP.Application.Empresas;
using NeoSTP.Application.Tesoreria;
using NeoSTP.Application.Tesoreria.Dtos;
using NeoSTP.Domain.Core.Tesoreria;

namespace NeoSTP.Web.Controllers;

/// <summary>NEOTESORERIA — cuentas de banco/caja y movimientos. Permisos Tesoreria.*.</summary>
[Authorize]
public class TesoreriaController : Controller
{
    public static readonly string[] TiposCuenta = TiposCuentaTesoreria.All;
    public static readonly string[] TiposMovimiento = TiposMovimientoTesoreria.All;

    private readonly ITesoreriaService _tesoreria;
    private readonly IConciliacionBancariaService _conciliacion;
    private readonly ICurrentUser _currentUser;
    private readonly IEmpresaContext _empresaContext;

    public TesoreriaController(ITesoreriaService tesoreria, IConciliacionBancariaService conciliacion, ICurrentUser currentUser, IEmpresaContext empresaContext)
    {
        _tesoreria = tesoreria;
        _conciliacion = conciliacion;
        _currentUser = currentUser;
        _empresaContext = empresaContext;
    }

    // ── Cuentas ──────────────────────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> Index(string? search, int page = 1, CancellationToken ct = default)
    {
        if (!Has("Tesoreria.Cuentas.Ver")) return Forbid();
        if (RequireEmpresa() is not int eid) return RedirectToSoporte();

        var result = await _tesoreria.ListCuentasAsync(eid, new PagedQuery { Search = search, Page = page, PageSize = 20 }, ct);
        var resumen = await _tesoreria.ResumenAsync(eid, ct);
        ViewBag.Search = search;
        ViewBag.Resumen = resumen.Value;
        ViewBag.PuedeGestionar = Has("Tesoreria.Cuentas.Gestionar");
        return View(result.Value);
    }

    [HttpGet]
    public async Task<IActionResult> Detalle(int id, CancellationToken ct)
    {
        if (!Has("Tesoreria.Cuentas.Ver")) return Forbid();
        if (RequireEmpresa() is not int eid) return RedirectToSoporte();

        var result = await _tesoreria.GetCuentaAsync(eid, id, ct);
        if (result.IsFailure) return NotFound();
        ViewBag.PuedeGestionar = Has("Tesoreria.Cuentas.Gestionar");
        ViewBag.PuedeMover = Has("Tesoreria.Movimientos.Gestionar");
        return View(result.Value);
    }

    [HttpGet]
    public IActionResult Create()
    {
        if (!Has("Tesoreria.Cuentas.Gestionar")) return Forbid();
        ViewBag.TiposCuenta = TiposCuenta;
        return View(new CreateCuentaTesoreriaRequest());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateCuentaTesoreriaRequest model, CancellationToken ct)
    {
        if (!Has("Tesoreria.Cuentas.Gestionar")) return Forbid();
        if (RequireEmpresa() is not int eid) return RedirectToSoporte();

        var result = await _tesoreria.CrearCuentaAsync(eid, model, _currentUser.Username, ct);
        if (result.IsFailure)
        {
            foreach (var e in result.ValidationErrors ?? new[] { result.Error ?? "Error." }) ModelState.AddModelError(string.Empty, e);
            ViewBag.TiposCuenta = TiposCuenta;
            return View(model);
        }
        TempData["Success"] = "Cuenta creada.";
        return RedirectToAction(nameof(Detalle), new { id = result.Value!.Id });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id, CancellationToken ct)
    {
        if (!Has("Tesoreria.Cuentas.Gestionar")) return Forbid();
        if (RequireEmpresa() is not int eid) return RedirectToSoporte();

        var result = await _tesoreria.GetCuentaAsync(eid, id, ct);
        if (result.IsFailure) return NotFound();
        var c = result.Value!;
        ViewBag.TiposCuenta = TiposCuenta;
        ViewBag.CuentaId = id;
        ViewBag.Codigo = c.Codigo;
        return View(new UpdateCuentaTesoreriaRequest
        {
            Nombre = c.Nombre, TipoCuenta = c.TipoCuenta, Banco = c.Banco, NumeroCuenta = c.NumeroCuenta,
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, UpdateCuentaTesoreriaRequest model, CancellationToken ct)
    {
        if (!Has("Tesoreria.Cuentas.Gestionar")) return Forbid();
        if (RequireEmpresa() is not int eid) return RedirectToSoporte();

        var result = await _tesoreria.ActualizarCuentaAsync(eid, id, model, _currentUser.Username, ct);
        if (result.IsFailure)
        {
            foreach (var e in result.ValidationErrors ?? new[] { result.Error ?? "Error." }) ModelState.AddModelError(string.Empty, e);
            ViewBag.TiposCuenta = TiposCuenta;
            ViewBag.CuentaId = id;
            return View(model);
        }
        TempData["Success"] = "Cuenta actualizada.";
        return RedirectToAction(nameof(Detalle), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Inactivar(int id, CancellationToken ct)
    {
        if (!Has("Tesoreria.Cuentas.Gestionar")) return Forbid();
        if (RequireEmpresa() is not int eid) return RedirectToSoporte();
        var result = await _tesoreria.InactivarCuentaAsync(eid, id, _currentUser.Username, ct);
        TempData[result.IsSuccess ? "Success" : "Error"] = result.IsSuccess ? "Cuenta inactivada." : result.Error;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reactivar(int id, CancellationToken ct)
    {
        if (!Has("Tesoreria.Cuentas.Gestionar")) return Forbid();
        if (RequireEmpresa() is not int eid) return RedirectToSoporte();
        var result = await _tesoreria.ReactivarCuentaAsync(eid, id, _currentUser.Username, ct);
        TempData[result.IsSuccess ? "Success" : "Error"] = result.IsSuccess ? "Cuenta reactivada." : result.Error;
        return RedirectToAction(nameof(Index));
    }

    // ── Movimientos ──────────────────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> Movimientos(int? cuentaId, string? search, int page = 1, CancellationToken ct = default)
    {
        if (!Has("Tesoreria.Movimientos.Ver")) return Forbid();
        if (RequireEmpresa() is not int eid) return RedirectToSoporte();

        var result = await _tesoreria.ListMovimientosAsync(eid, cuentaId, new PagedQuery { Search = search, Page = page, PageSize = 30 }, ct);
        ViewBag.Search = search;
        ViewBag.CuentaId = cuentaId;
        ViewBag.PuedeGestionar = Has("Tesoreria.Movimientos.Gestionar");
        return View(result.Value);
    }

    [HttpGet]
    public async Task<IActionResult> RegistrarMovimiento(int? cuentaId, CancellationToken ct)
    {
        if (!Has("Tesoreria.Movimientos.Gestionar")) return Forbid();
        if (RequireEmpresa() is not int eid) return RedirectToSoporte();

        await CargarCuentasAsync(eid, ct);
        ViewBag.TiposMovimiento = TiposMovimiento;
        return View(new RegistrarMovimientoRequest { CuentaId = cuentaId ?? 0, Fecha = DateOnly.FromDateTime(DateTime.UtcNow) });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RegistrarMovimiento(RegistrarMovimientoRequest model, CancellationToken ct)
    {
        if (!Has("Tesoreria.Movimientos.Gestionar")) return Forbid();
        if (RequireEmpresa() is not int eid) return RedirectToSoporte();

        var result = await _tesoreria.RegistrarMovimientoAsync(eid, model, _currentUser.Username, ct);
        if (result.IsFailure)
        {
            foreach (var e in result.ValidationErrors ?? new[] { result.Error ?? "Error." }) ModelState.AddModelError(string.Empty, e);
            await CargarCuentasAsync(eid, ct);
            ViewBag.TiposMovimiento = TiposMovimiento;
            return View(model);
        }
        TempData["Success"] = "Movimiento registrado.";
        return RedirectToAction(nameof(Detalle), new { id = model.CuentaId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AnularMovimiento(int id, int cuentaId, CancellationToken ct)
    {
        if (!Has("Tesoreria.Movimientos.Gestionar")) return Forbid();
        if (RequireEmpresa() is not int eid) return RedirectToSoporte();
        var result = await _tesoreria.AnularMovimientoAsync(eid, id, _currentUser.Username, ct);
        TempData[result.IsSuccess ? "Success" : "Error"] = result.IsSuccess ? "Movimiento anulado." : result.Error;
        return RedirectToAction(nameof(Detalle), new { id = cuentaId });
    }

    // ── Conciliación bancaria (V2-D4) ────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> Conciliacion(int? cuentaId, string? estado, int page = 1, CancellationToken ct = default)
    {
        if (!Has("Tesoreria.Movimientos.Ver")) return Forbid();
        if (RequireEmpresa() is not int eid) return RedirectToSoporte();

        await CargarCuentasAsync(eid, ct);
        var cuentas = (List<CuentaTesoreriaDto>)ViewBag.Cuentas!;
        var cuentaSel = cuentaId ?? cuentas.FirstOrDefault()?.Id;
        ViewBag.CuentaId = cuentaSel;
        ViewBag.Estado = estado;
        ViewBag.PuedeGestionar = Has("Tesoreria.Movimientos.Gestionar");
        if (cuentaSel is not int cid) return View(new ConciliacionViewModel());

        var movimientos = await _conciliacion.ListAsync(eid, cid, estado, new PagedQuery { Page = page, PageSize = 30 }, ct);
        var resumen = await _conciliacion.ResumenAsync(eid, cid, ct);
        var sugerencias = await _conciliacion.SugerenciasAsync(eid, cid, ct: ct);
        return View(new ConciliacionViewModel
        {
            Movimientos = movimientos.Value,
            Resumen = resumen.Value,
            Sugerencias = sugerencias.Value?.ToDictionary(s => s.MovimientoBancoId) ?? [],
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(10_000_000)]
    public async Task<IActionResult> ImportarEstadoCuenta(int cuentaId, IFormFile? archivo, CancellationToken ct)
    {
        if (!Has("Tesoreria.Movimientos.Gestionar")) return Forbid();
        if (RequireEmpresa() is not int eid) return RedirectToSoporte();
        if (archivo is null || archivo.Length == 0)
        {
            TempData["Error"] = "Selecciona un archivo CSV o Excel.";
            return RedirectToAction(nameof(Conciliacion), new { cuentaId });
        }

        var fmt = archivo.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase)
            ? NeoSTP.Application.Common.BulkFileFormat.Csv : NeoSTP.Application.Common.BulkFileFormat.Xlsx;
        using var ms = new MemoryStream();
        await archivo.CopyToAsync(ms, ct);
        ms.Position = 0;

        var result = await _conciliacion.ImportarAsync(eid, cuentaId, new NeoSTP.Application.Common.BulkImportRequest { Format = fmt, Content = ms }, _currentUser.Username, ct);
        if (result.IsFailure) TempData["Error"] = result.Error;
        else
        {
            var r = result.Value!;
            TempData["Success"] = $"Importadas {r.Inserted} línea(s); {r.Skipped} duplicada(s) omitida(s)" +
                (r.ErrorCount > 0 ? $"; {r.ErrorCount} fila(s) con error (revisa formato fecha/monto)." : ".");
        }
        return RedirectToAction(nameof(Conciliacion), new { cuentaId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Conciliar(int id, int movimientoTesoreriaId, int cuentaId, CancellationToken ct)
    {
        if (!Has("Tesoreria.Movimientos.Gestionar")) return Forbid();
        if (RequireEmpresa() is not int eid) return RedirectToSoporte();
        var result = await _conciliacion.ConciliarAsync(eid, id, movimientoTesoreriaId, _currentUser.Username, ct);
        TempData[result.IsSuccess ? "Success" : "Error"] = result.IsSuccess ? "Línea conciliada." : result.Error;
        return RedirectToAction(nameof(Conciliacion), new { cuentaId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ConciliarSugeridos(int cuentaId, CancellationToken ct)
    {
        if (!Has("Tesoreria.Movimientos.Gestionar")) return Forbid();
        if (RequireEmpresa() is not int eid) return RedirectToSoporte();
        var result = await _conciliacion.ConciliarSugeridosAsync(eid, cuentaId, actor: _currentUser.Username, ct: ct);
        TempData[result.IsSuccess ? "Success" : "Error"] = result.IsSuccess
            ? $"Conciliadas {result.Value} línea(s) con confianza alta." : result.Error;
        return RedirectToAction(nameof(Conciliacion), new { cuentaId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Desconciliar(int id, int cuentaId, CancellationToken ct)
    {
        if (!Has("Tesoreria.Movimientos.Gestionar")) return Forbid();
        if (RequireEmpresa() is not int eid) return RedirectToSoporte();
        var result = await _conciliacion.DesconciliarAsync(eid, id, _currentUser.Username, ct);
        TempData[result.IsSuccess ? "Success" : "Error"] = result.IsSuccess ? "Línea desconciliada." : result.Error;
        return RedirectToAction(nameof(Conciliacion), new { cuentaId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ConciliarCombinacion(int id, int cuentaId, int[] movimientoTesoreriaIds, CancellationToken ct)
    {
        if (!Has("Tesoreria.Movimientos.Gestionar")) return Forbid();
        if (RequireEmpresa() is not int eid) return RedirectToSoporte();
        var result = await _conciliacion.ConciliarCombinacionAsync(eid, id, movimientoTesoreriaIds, _currentUser.Username, ct);
        TempData[result.IsSuccess ? "Success" : "Error"] = result.IsSuccess ? "Movimientos aplicados a la línea." : result.Error;
        return RedirectToAction(nameof(Conciliacion), new { cuentaId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> QuitarDetalle(int id, int movimientoTesoreriaId, int cuentaId, CancellationToken ct)
    {
        if (!Has("Tesoreria.Movimientos.Gestionar")) return Forbid();
        if (RequireEmpresa() is not int eid) return RedirectToSoporte();
        var result = await _conciliacion.QuitarDetalleAsync(eid, id, movimientoTesoreriaId, _currentUser.Username, ct);
        TempData[result.IsSuccess ? "Success" : "Error"] = result.IsSuccess ? "Movimiento removido." : result.Error;
        return RedirectToAction(nameof(Conciliacion), new { cuentaId });
    }

    private async Task CargarCuentasAsync(int eid, CancellationToken ct)
    {
        var cuentas = await _tesoreria.ListCuentasAsync(eid, new PagedQuery { Page = 1, PageSize = 200 }, ct);
        ViewBag.Cuentas = cuentas.Value?.Items
            .Where(c => c.EstadoCodigo == EstadosCuentaTesoreria.Activa)
            .ToList() ?? new List<CuentaTesoreriaDto>();
    }

    private bool Has(string codigo) => _currentUser.TipoUsuarioCodigo == "SUPERADMIN" || _currentUser.HasPermiso(codigo);
    private int? RequireEmpresa() => _empresaContext.CurrentEmpresaId;

    private IActionResult RedirectToSoporte()
    {
        if (_currentUser.TipoUsuarioCodigo == "SUPERADMIN")
        {
            TempData["Error"] = "La tesorería opera dentro de una empresa. Selecciona una en modo soporte primero.";
            return RedirectToAction("Index", "Soporte");
        }
        return RedirectToAction("Index", "Home");
    }
}

public class ConciliacionViewModel
{
    public PagedResult<MovimientoBancarioDto>? Movimientos { get; set; }
    public ConciliacionResumenDto? Resumen { get; set; }

    /// <summary>Sugerencia de match por MovimientoBancoId (solo líneas NO_CONCILIADO).</summary>
    public Dictionary<int, SugerenciaConciliacionDto> Sugerencias { get; set; } = [];
}
