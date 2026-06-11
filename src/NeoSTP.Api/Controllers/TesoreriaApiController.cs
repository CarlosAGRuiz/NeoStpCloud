using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NeoSTP.Api.Authorization;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Common;
using NeoSTP.Application.Tesoreria;
using NeoSTP.Application.Tesoreria.Dtos;
using NeoSTP.Shared;

namespace NeoSTP.Api.Controllers;

/// <summary>
/// NEOTESORERIA — cuentas (banco/caja) y movimientos (API-first, consumible por la app y la web).
/// Requiere el módulo NEOTESORERIA activo. Cuentas: Tesoreria.Cuentas.*; movimientos: Tesoreria.Movimientos.*.
/// </summary>
[Authorize]
[RequireModule("NEOTESORERIA")]
[Route("api/tesoreria")]
public class TesoreriaApiController : ApiControllerBase
{
    private readonly ITesoreriaService _tesoreria;
    private readonly IConciliacionBancariaService _conciliacion;
    private readonly ICurrentUser _currentUser;

    public TesoreriaApiController(ITesoreriaService tesoreria, IConciliacionBancariaService conciliacion, ICurrentUser currentUser)
    {
        _tesoreria = tesoreria;
        _conciliacion = conciliacion;
        _currentUser = currentUser;
    }

    // ── Cuentas ──────────────────────────────────────────────────────────────

    [HttpGet("cuentas")]
    [RequirePermiso("Tesoreria.Cuentas.Ver")]
    public async Task<IActionResult> ListCuentas([FromQuery] PagedQuery query, [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        return Respond(await _tesoreria.ListCuentasAsync(eid, query, ct));
    }

    [HttpGet("cuentas/{id:int}")]
    [RequirePermiso("Tesoreria.Cuentas.Ver")]
    public async Task<IActionResult> GetCuenta(int id, [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        return Respond(await _tesoreria.GetCuentaAsync(eid, id, ct));
    }

    [HttpPost("cuentas")]
    [RequirePermiso("Tesoreria.Cuentas.Gestionar")]
    public async Task<IActionResult> CrearCuenta([FromBody] CreateCuentaTesoreriaRequest req, [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        return Respond(await _tesoreria.CrearCuentaAsync(eid, req, _currentUser.Username, ct));
    }

    [HttpPut("cuentas/{id:int}")]
    [RequirePermiso("Tesoreria.Cuentas.Gestionar")]
    public async Task<IActionResult> EditarCuenta(int id, [FromBody] UpdateCuentaTesoreriaRequest req, [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        return Respond(await _tesoreria.ActualizarCuentaAsync(eid, id, req, _currentUser.Username, ct));
    }

    [HttpPost("cuentas/{id:int}/inactivar")]
    [RequirePermiso("Tesoreria.Cuentas.Gestionar")]
    public async Task<IActionResult> InactivarCuenta(int id, [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        return Respond(await _tesoreria.InactivarCuentaAsync(eid, id, _currentUser.Username, ct), "Cuenta inactivada.");
    }

    [HttpPost("cuentas/{id:int}/reactivar")]
    [RequirePermiso("Tesoreria.Cuentas.Gestionar")]
    public async Task<IActionResult> ReactivarCuenta(int id, [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        return Respond(await _tesoreria.ReactivarCuentaAsync(eid, id, _currentUser.Username, ct), "Cuenta reactivada.");
    }

    // ── Movimientos ──────────────────────────────────────────────────────────

    [HttpGet("movimientos")]
    [RequirePermiso("Tesoreria.Movimientos.Ver")]
    public async Task<IActionResult> ListMovimientos([FromQuery] PagedQuery query, [FromQuery] int? cuentaId, [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        return Respond(await _tesoreria.ListMovimientosAsync(eid, cuentaId, query, ct));
    }

    [HttpPost("movimientos")]
    [RequirePermiso("Tesoreria.Movimientos.Gestionar")]
    public async Task<IActionResult> RegistrarMovimiento([FromBody] RegistrarMovimientoRequest req, [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        return Respond(await _tesoreria.RegistrarMovimientoAsync(eid, req, _currentUser.Username, ct));
    }

    [HttpPost("movimientos/{id:int}/anular")]
    [RequirePermiso("Tesoreria.Movimientos.Gestionar")]
    public async Task<IActionResult> AnularMovimiento(int id, [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        return Respond(await _tesoreria.AnularMovimientoAsync(eid, id, _currentUser.Username, ct), "Movimiento anulado.");
    }

    [HttpGet("resumen")]
    [RequirePermiso("Tesoreria.Cuentas.Ver")]
    public async Task<IActionResult> Resumen([FromQuery] int? empresaId, CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        return Respond(await _tesoreria.ResumenAsync(eid, ct));
    }

    // ── Conciliación bancaria (V2-D4) ────────────────────────────────────────

    /// <summary>Importa el estado de cuenta del banco (multipart: archivo CSV/XLSX).</summary>
    [HttpPost("conciliacion/{cuentaId:int}/importar")]
    [RequirePermiso("Tesoreria.Movimientos.Gestionar")]
    [RequestSizeLimit(10_000_000)]
    public async Task<IActionResult> ImportarEstadoCuenta(int cuentaId, IFormFile? archivo, [FromQuery] bool dryRun, [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        if (archivo is null || archivo.Length == 0)
            return BadRequest(ApiResponse.Fail("Adjunta el archivo CSV o Excel en el campo 'archivo'.", null, HttpContext.TraceIdentifier));

        var fmt = archivo.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase) ? BulkFileFormat.Csv : BulkFileFormat.Xlsx;
        using var ms = new MemoryStream();
        await archivo.CopyToAsync(ms, ct);
        ms.Position = 0;
        return Respond(await _conciliacion.ImportarAsync(eid, cuentaId, new BulkImportRequest { Format = fmt, Content = ms, DryRun = dryRun }, _currentUser.Username, ct));
    }

    [HttpGet("conciliacion/{cuentaId:int}/movimientos")]
    [RequirePermiso("Tesoreria.Movimientos.Ver")]
    public async Task<IActionResult> ListMovimientosBanco(int cuentaId, [FromQuery] PagedQuery query, [FromQuery] string? estado, [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        return Respond(await _conciliacion.ListAsync(eid, cuentaId, estado, query, ct));
    }

    [HttpGet("conciliacion/{cuentaId:int}/sugerencias")]
    [RequirePermiso("Tesoreria.Movimientos.Ver")]
    public async Task<IActionResult> Sugerencias(int cuentaId, [FromQuery] int toleranciaDias = 3, [FromQuery] int? empresaId = null, CancellationToken ct = default)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        return Respond(await _conciliacion.SugerenciasAsync(eid, cuentaId, toleranciaDias, ct));
    }

    [HttpGet("conciliacion/{cuentaId:int}/resumen")]
    [RequirePermiso("Tesoreria.Movimientos.Ver")]
    public async Task<IActionResult> ResumenConciliacion(int cuentaId, [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        return Respond(await _conciliacion.ResumenAsync(eid, cuentaId, ct));
    }

    [HttpPost("conciliacion/movimientos/{id:int}/conciliar/{movimientoTesoreriaId:int}")]
    [RequirePermiso("Tesoreria.Movimientos.Gestionar")]
    public async Task<IActionResult> Conciliar(int id, int movimientoTesoreriaId, [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        return Respond(await _conciliacion.ConciliarAsync(eid, id, movimientoTesoreriaId, _currentUser.Username, ct), "Línea conciliada.");
    }

    /// <summary>Aplica todas las sugerencias de confianza ALTA de la cuenta.</summary>
    [HttpPost("conciliacion/{cuentaId:int}/conciliar-sugeridos")]
    [RequirePermiso("Tesoreria.Movimientos.Gestionar")]
    public async Task<IActionResult> ConciliarSugeridos(int cuentaId, [FromQuery] int toleranciaDias = 3, [FromQuery] int? empresaId = null, CancellationToken ct = default)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        return Respond(await _conciliacion.ConciliarSugeridosAsync(eid, cuentaId, toleranciaDias, _currentUser.Username, ct));
    }

    [HttpPost("conciliacion/movimientos/{id:int}/desconciliar")]
    [RequirePermiso("Tesoreria.Movimientos.Gestionar")]
    public async Task<IActionResult> Desconciliar(int id, [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        return Respond(await _conciliacion.DesconciliarAsync(eid, id, _currentUser.Username, ct), "Línea desconciliada.");
    }

    /// <summary>V2.5-S1: aplica varios movimientos internos a una línea (conciliación N:1).</summary>
    [HttpPost("conciliacion/movimientos/{id:int}/conciliar-combinacion")]
    [RequirePermiso("Tesoreria.Movimientos.Gestionar")]
    public async Task<IActionResult> ConciliarCombinacion(int id, [FromBody] int[] movimientoTesoreriaIds, [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        return Respond(await _conciliacion.ConciliarCombinacionAsync(eid, id, movimientoTesoreriaIds, _currentUser.Username, ct), "Movimientos aplicados.");
    }

    /// <summary>V2.5-S1: quita un movimiento interno de una línea conciliada o parcial.</summary>
    [HttpPost("conciliacion/movimientos/{id:int}/quitar/{movimientoTesoreriaId:int}")]
    [RequirePermiso("Tesoreria.Movimientos.Gestionar")]
    public async Task<IActionResult> QuitarDetalle(int id, int movimientoTesoreriaId, [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        return Respond(await _conciliacion.QuitarDetalleAsync(eid, id, movimientoTesoreriaId, _currentUser.Username, ct), "Movimiento removido de la línea.");
    }

    private int? Resolve(int? fromRequest) => _currentUser.EmpresaId ?? fromRequest;

    private object NoTenant() => ApiResponse.Fail(
        "No se pudo determinar la empresa. Si eres SuperAdmin, envía empresaId.",
        new[] { "AUTH_NO_TENANT" }, HttpContext.TraceIdentifier);
}
