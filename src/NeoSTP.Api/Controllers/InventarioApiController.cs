using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NeoSTP.Api.Authorization;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Common;
using NeoSTP.Application.Inventario;
using NeoSTP.Application.Inventario.Dtos;
using NeoSTP.Shared;

namespace NeoSTP.Api.Controllers;

/// <summary>
/// INVENTARIO — existencias y kardex (API-first). Requiere el módulo INVENTARIO activo.
/// Ver: Inventario.Ver; movimientos/ajustes: Inventario.Gestionar.
/// </summary>
[Authorize]
[RequireModule("INVENTARIO")]
[Route("api/inventario")]
public class InventarioApiController : ApiControllerBase
{
    private readonly IInventarioService _inv;
    private readonly ICurrentUser _currentUser;

    public InventarioApiController(IInventarioService inv, ICurrentUser currentUser)
    {
        _inv = inv;
        _currentUser = currentUser;
    }

    [HttpGet("existencias")]
    [RequirePermiso("Inventario.Ver")]
    public async Task<IActionResult> ListExistencias([FromQuery] PagedQuery query, [FromQuery] bool soloStockBajo, [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        return Respond(await _inv.ListExistenciasAsync(eid, soloStockBajo, query, ct));
    }

    [HttpGet("existencias/{productoId:int}")]
    [RequirePermiso("Inventario.Ver")]
    public async Task<IActionResult> GetExistencia(int productoId, [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        return Respond(await _inv.GetExistenciaAsync(eid, productoId, ct));
    }

    [HttpGet("kardex/{productoId:int}")]
    [RequirePermiso("Inventario.Ver")]
    public async Task<IActionResult> Kardex(int productoId, [FromQuery] PagedQuery query, [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        return Respond(await _inv.GetKardexAsync(eid, productoId, query, ct));
    }

    [HttpPost("entradas")]
    [RequirePermiso("Inventario.Gestionar")]
    public async Task<IActionResult> Entrada([FromBody] RegistrarMovimientoInventarioRequest req, [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        return Respond(await _inv.RegistrarEntradaAsync(eid, req, _currentUser.Username, ct));
    }

    [HttpPost("salidas")]
    [RequirePermiso("Inventario.Gestionar")]
    public async Task<IActionResult> Salida([FromBody] RegistrarMovimientoInventarioRequest req, [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        return Respond(await _inv.RegistrarSalidaAsync(eid, req, _currentUser.Username, ct));
    }

    [HttpPost("ajustes")]
    [RequirePermiso("Inventario.Gestionar")]
    public async Task<IActionResult> Ajuste([FromBody] AjusteStockRequest req, [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        return Respond(await _inv.AjustarAsync(eid, req, _currentUser.Username, ct));
    }

    [HttpPost("stock-minimo")]
    [RequirePermiso("Inventario.Gestionar")]
    public async Task<IActionResult> StockMinimo([FromBody] SetStockMinimoRequest req, [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        return Respond(await _inv.SetStockMinimoAsync(eid, req, _currentUser.Username, ct));
    }

    [HttpGet("resumen")]
    [RequirePermiso("Inventario.Ver")]
    public async Task<IActionResult> Resumen([FromQuery] int? empresaId, CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        return Respond(await _inv.ResumenAsync(eid, ct));
    }

    private int? Resolve(int? fromRequest) => _currentUser.EmpresaId ?? fromRequest;

    private object NoTenant() => ApiResponse.Fail(
        "No se pudo determinar la empresa. Si eres SuperAdmin, envía empresaId.",
        new[] { "AUTH_NO_TENANT" }, HttpContext.TraceIdentifier);
}
