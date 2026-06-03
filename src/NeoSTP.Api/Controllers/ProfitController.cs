using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NeoSTP.Api.Authorization;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Profit;
using NeoSTP.Application.Profit.Dtos;
using NeoSTP.Shared;

namespace NeoSTP.Api.Controllers;

/// <summary>
/// NeoProfit — métricas financieras (ventas, IVA, rentabilidad, rankings) y registro de gastos/compras.
/// Requiere el módulo NEOPROFIT activo. Lectura: Profit.Ver; escritura: Profit.Gestionar.
/// </summary>
[Authorize]
[RequireModule("NEOPROFIT")]
[Route("api/profit")]
public class ProfitController : ApiControllerBase
{
    private readonly IProfitService _service;
    private readonly ICurrentUser _currentUser;

    public ProfitController(IProfitService service, ICurrentUser currentUser)
    {
        _service = service;
        _currentUser = currentUser;
    }

    // ─── Lectura (Profit.Ver) ────────────────────────────────────────────────

    [HttpGet("dashboard")]
    [RequirePermiso("Profit.Ver")]
    public async Task<IActionResult> Dashboard([FromQuery] ProfitPeriodoQuery periodo, [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        return Ok(ApiResponse<ProfitDashboardDto>.Ok(await _service.GetDashboardAsync(eid, periodo, ct), traceId: HttpContext.TraceIdentifier));
    }

    [HttpGet("productos")]
    [RequirePermiso("Profit.Ver")]
    public async Task<IActionResult> Productos([FromQuery] ProfitPeriodoQuery periodo, [FromQuery] int top = 20, [FromQuery] int? empresaId = null, CancellationToken ct = default)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        return Ok(ApiResponse<IReadOnlyList<ProfitProductoDto>>.Ok(await _service.GetProductosAsync(eid, periodo, top, ct), traceId: HttpContext.TraceIdentifier));
    }

    [HttpGet("clientes")]
    [RequirePermiso("Profit.Ver")]
    public async Task<IActionResult> Clientes([FromQuery] ProfitPeriodoQuery periodo, [FromQuery] int top = 20, [FromQuery] int? empresaId = null, CancellationToken ct = default)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        return Ok(ApiResponse<IReadOnlyList<ProfitClienteDto>>.Ok(await _service.GetClientesAsync(eid, periodo, top, ct), traceId: HttpContext.TraceIdentifier));
    }

    [HttpGet("sucursales")]
    [RequirePermiso("Profit.Ver")]
    public async Task<IActionResult> Sucursales([FromQuery] ProfitPeriodoQuery periodo, [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        return Ok(ApiResponse<IReadOnlyList<ProfitSucursalDto>>.Ok(await _service.GetSucursalesAsync(eid, periodo, ct), traceId: HttpContext.TraceIdentifier));
    }

    [HttpGet("tendencia")]
    [RequirePermiso("Profit.Ver")]
    public async Task<IActionResult> Tendencia([FromQuery] int dias = 30, [FromQuery] int? empresaId = null, CancellationToken ct = default)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        return Ok(ApiResponse<IReadOnlyList<ProfitTendenciaPuntoDto>>.Ok(await _service.GetTendenciaAsync(eid, dias, ct), traceId: HttpContext.TraceIdentifier));
    }

    // ─── Gastos ──────────────────────────────────────────────────────────────

    [HttpGet("gastos")]
    [RequirePermiso("Profit.Ver")]
    public async Task<IActionResult> ListGastos([FromQuery] NeoSTP.Application.Common.PagedQuery query, [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        return Respond(await _service.ListGastosAsync(eid, query, ct));
    }

    [HttpPost("gastos")]
    [RequirePermiso("Profit.Gestionar")]
    public async Task<IActionResult> CrearGasto([FromBody] CreateProfitGastoRequest req, [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        return Respond(await _service.CreateGastoAsync(eid, req, _currentUser.Username, ct));
    }

    [HttpPut("gastos/{id:int}")]
    [RequirePermiso("Profit.Gestionar")]
    public async Task<IActionResult> EditarGasto(int id, [FromBody] UpdateProfitGastoRequest req, [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        return Respond(await _service.UpdateGastoAsync(eid, id, req, _currentUser.Username, ct));
    }

    [HttpDelete("gastos/{id:int}")]
    [RequirePermiso("Profit.Gestionar")]
    public async Task<IActionResult> InactivarGasto(int id, [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        return Respond(await _service.InactivarGastoAsync(eid, id, _currentUser.Username, ct), "Gasto inactivado.");
    }

    // ─── Compras ─────────────────────────────────────────────────────────────

    [HttpGet("compras")]
    [RequirePermiso("Profit.Ver")]
    public async Task<IActionResult> ListCompras([FromQuery] NeoSTP.Application.Common.PagedQuery query, [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        return Respond(await _service.ListComprasAsync(eid, query, ct));
    }

    [HttpPost("compras")]
    [RequirePermiso("Profit.Gestionar")]
    public async Task<IActionResult> CrearCompra([FromBody] CreateProfitCompraRequest req, [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        return Respond(await _service.CreateCompraAsync(eid, req, _currentUser.Username, ct));
    }

    [HttpPut("compras/{id:int}")]
    [RequirePermiso("Profit.Gestionar")]
    public async Task<IActionResult> EditarCompra(int id, [FromBody] UpdateProfitCompraRequest req, [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        return Respond(await _service.UpdateCompraAsync(eid, id, req, _currentUser.Username, ct));
    }

    [HttpDelete("compras/{id:int}")]
    [RequirePermiso("Profit.Gestionar")]
    public async Task<IActionResult> InactivarCompra(int id, [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        return Respond(await _service.InactivarCompraAsync(eid, id, _currentUser.Username, ct), "Compra inactivada.");
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private int? Resolve(int? fromRequest) => _currentUser.EmpresaId ?? fromRequest;

    private object NoTenant() => ApiResponse.Fail(
        "No se pudo determinar la empresa. Si eres SuperAdmin, envía empresaId.",
        new[] { "AUTH_NO_TENANT" }, HttpContext.TraceIdentifier);
}
