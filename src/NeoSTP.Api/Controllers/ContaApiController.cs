using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NeoSTP.Api.Authorization;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Common;
using NeoSTP.Application.Conta;
using NeoSTP.Shared;

namespace NeoSTP.Api.Controllers;

/// <summary>
/// NEOCONTA — asientos contables básicos y balanza (API-first). Requiere el módulo
/// NEOCONTA activo. Ver: Conta.Ver; generar/reversar: Conta.Gestionar.
/// </summary>
[Authorize]
[RequireModule("NEOCONTA")]
[Route("api/conta")]
public class ContaApiController : ApiControllerBase
{
    private readonly IContabilidadService _conta;
    private readonly ICurrentUser _currentUser;

    public ContaApiController(IContabilidadService conta, ICurrentUser currentUser)
    {
        _conta = conta;
        _currentUser = currentUser;
    }

    [HttpGet("cuentas")]
    [RequirePermiso("Conta.Ver")]
    public async Task<IActionResult> ListCuentas([FromQuery] int? empresaId, CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        return Respond(await _conta.ListCuentasAsync(eid, ct));
    }

    /// <summary>Genera los asientos automáticos del período (idempotente).</summary>
    [HttpPost("asientos/generar")]
    [RequirePermiso("Conta.Gestionar")]
    public async Task<IActionResult> GenerarAsientos([FromQuery] int anio, [FromQuery] int mes, [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        return Respond(await _conta.GenerarAsientosPeriodoAsync(eid, anio, mes, _currentUser.Username, ct));
    }

    [HttpGet("asientos")]
    [RequirePermiso("Conta.Ver")]
    public async Task<IActionResult> ListAsientos([FromQuery] PagedQuery query, [FromQuery] int? anio, [FromQuery] int? mes, [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        return Respond(await _conta.ListAsientosAsync(eid, anio, mes, query, ct));
    }

    [HttpGet("asientos/{id:int}")]
    [RequirePermiso("Conta.Ver")]
    public async Task<IActionResult> GetAsiento(int id, [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        return Respond(await _conta.GetAsientoAsync(eid, id, ct));
    }

    /// <summary>Reversa un asiento (crea el asiento espejo; nunca borra).</summary>
    [HttpPost("asientos/{id:int}/reversar")]
    [RequirePermiso("Conta.Gestionar")]
    public async Task<IActionResult> ReversarAsiento(int id, [FromQuery] string? motivo, [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        return Respond(await _conta.ReversarAsientoAsync(eid, id, motivo, _currentUser.Username, ct));
    }

    [HttpGet("balanza")]
    [RequirePermiso("Conta.Ver")]
    public async Task<IActionResult> Balanza([FromQuery] int anio, [FromQuery] int mes, [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        return Respond(await _conta.BalanzaAsync(eid, anio, mes, ct));
    }

    [HttpGet("balanza/csv")]
    [RequirePermiso("Conta.Ver")]
    public async Task<IActionResult> BalanzaCsv([FromQuery] int anio, [FromQuery] int mes, [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        var r = await _conta.BalanzaCsvAsync(eid, anio, mes, ct);
        if (r.IsFailure) return Respond(r);
        return File(r.Value!, "text/csv", $"balanza_{anio:0000}_{mes:00}.csv");
    }

    private int? Resolve(int? fromRequest) => _currentUser.EmpresaId ?? fromRequest;

    private object NoTenant() => ApiResponse.Fail(
        "No se pudo determinar la empresa. Si eres SuperAdmin, envía empresaId.",
        new[] { "AUTH_NO_TENANT" }, HttpContext.TraceIdentifier);
}
