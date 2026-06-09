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
    private readonly ICurrentUser _currentUser;

    public TesoreriaApiController(ITesoreriaService tesoreria, ICurrentUser currentUser)
    {
        _tesoreria = tesoreria;
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

    private int? Resolve(int? fromRequest) => _currentUser.EmpresaId ?? fromRequest;

    private object NoTenant() => ApiResponse.Fail(
        "No se pudo determinar la empresa. Si eres SuperAdmin, envía empresaId.",
        new[] { "AUTH_NO_TENANT" }, HttpContext.TraceIdentifier);
}
