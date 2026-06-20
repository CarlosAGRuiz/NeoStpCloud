using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NeoSTP.Api.Authorization;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Common;
using NeoSTP.Application.Compras;
using NeoSTP.Application.Compras.Dtos;
using NeoSTP.Shared;

namespace NeoSTP.Api.Controllers;

/// <summary>
/// NEOCOMPRAS — proveedores y cuentas por pagar (API-first). Requiere el módulo COMPRAS activo.
/// Proveedores: Compras.Proveedores.*; facturas/pagos: Compras.Ver / Compras.Gestionar.
/// </summary>
[Authorize]
[RequireModule("COMPRAS")]
[Route("api/compras")]
public class ComprasApiController : ApiControllerBase
{
    private readonly ICompraService _compras;
    private readonly IOrdenCompraService _ordenes;
    private readonly ICurrentUser _currentUser;

    public ComprasApiController(ICompraService compras, IOrdenCompraService ordenes, ICurrentUser currentUser)
    {
        _compras = compras;
        _ordenes = ordenes;
        _currentUser = currentUser;
    }

    // ── Proveedores ──────────────────────────────────────────────────────────

    [HttpGet("proveedores")]
    [RequirePermiso("Compras.Proveedores.Ver")]
    public async Task<IActionResult> ListProveedores([FromQuery] PagedQuery query, [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        return Respond(await _compras.ListProveedoresAsync(eid, query, ct));
    }

    [HttpGet("proveedores/{id:int}")]
    [RequirePermiso("Compras.Proveedores.Ver")]
    public async Task<IActionResult> GetProveedor(int id, [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        return Respond(await _compras.GetProveedorAsync(eid, id, ct));
    }

    [HttpPost("proveedores")]
    [RequirePermiso("Compras.Proveedores.Gestionar")]
    public async Task<IActionResult> CrearProveedor([FromBody] CreateProveedorRequest req, [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        return Respond(await _compras.CrearProveedorAsync(eid, req, _currentUser.Username, ct));
    }

    [HttpPut("proveedores/{id:int}")]
    [RequirePermiso("Compras.Proveedores.Gestionar")]
    public async Task<IActionResult> EditarProveedor(int id, [FromBody] UpdateProveedorRequest req, [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        return Respond(await _compras.ActualizarProveedorAsync(eid, id, req, _currentUser.Username, ct));
    }

    [HttpPost("proveedores/{id:int}/inactivar")]
    [RequirePermiso("Compras.Proveedores.Gestionar")]
    public async Task<IActionResult> InactivarProveedor(int id, [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        return Respond(await _compras.InactivarProveedorAsync(eid, id, _currentUser.Username, ct), "Proveedor inactivado.");
    }

    [HttpPost("proveedores/{id:int}/reactivar")]
    [RequirePermiso("Compras.Proveedores.Gestionar")]
    public async Task<IActionResult> ReactivarProveedor(int id, [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        return Respond(await _compras.ReactivarProveedorAsync(eid, id, _currentUser.Username, ct), "Proveedor reactivado.");
    }

    // ── Facturas / CxP ─────────────────────────────────────────────────────────

    [HttpGet("ordenes")]
    [RequirePermiso("Compras.Ver")]
    public async Task<IActionResult> ListOrdenes(
        [FromQuery] PagedQuery query, [FromQuery] string? estado, [FromQuery] int? proveedorId,
        [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        return Respond(await _ordenes.ListAsync(eid, estado, proveedorId, query, ct));
    }

    [HttpGet("ordenes/{id:int}")]
    [RequirePermiso("Compras.Ver")]
    public async Task<IActionResult> GetOrden(int id, [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        return Respond(await _ordenes.GetAsync(eid, id, ct));
    }

    [HttpPost("ordenes")]
    [RequirePermiso("Compras.Gestionar")]
    public async Task<IActionResult> CrearOrden(
        [FromBody] GuardarOrdenCompraRequest req, [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        return Respond(await _ordenes.CrearAsync(eid, req, _currentUser.Username, ct));
    }

    [HttpPut("ordenes/{id:int}")]
    [RequirePermiso("Compras.Gestionar")]
    public async Task<IActionResult> ActualizarOrden(
        int id, [FromBody] GuardarOrdenCompraRequest req, [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        return Respond(await _ordenes.ActualizarAsync(eid, id, req, _currentUser.Username, ct));
    }

    [HttpPost("ordenes/{id:int}/emitir")]
    [RequirePermiso("Compras.Gestionar")]
    public async Task<IActionResult> EmitirOrden(int id, [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        return Respond(await _ordenes.EmitirAsync(eid, id, _currentUser.Username, ct));
    }

    [HttpPost("ordenes/{id:int}/cancelar")]
    [RequirePermiso("Compras.Gestionar")]
    public async Task<IActionResult> CancelarOrden(int id, [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        return Respond(await _ordenes.CancelarAsync(eid, id, _currentUser.Username, ct));
    }

    [HttpPost("ordenes/{id:int}/convertir-factura")]
    [RequirePermiso("Compras.Gestionar")]
    public async Task<IActionResult> ConvertirOrdenAFactura(
        int id, [FromBody] ConvertirOrdenCompraRequest req, [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        return Respond(await _ordenes.ConvertirAFacturaAsync(eid, id, req, _currentUser.Username, ct));
    }

    [HttpGet("facturas")]
    [RequirePermiso("Compras.Ver")]
    public async Task<IActionResult> ListFacturas([FromQuery] PagedQuery query, [FromQuery] int? proveedorId, [FromQuery] bool soloPendientes, [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        return Respond(await _compras.ListFacturasAsync(eid, proveedorId, soloPendientes, query, ct));
    }

    [HttpGet("facturas/{id:int}")]
    [RequirePermiso("Compras.Ver")]
    public async Task<IActionResult> GetFactura(int id, [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        return Respond(await _compras.GetFacturaAsync(eid, id, ct));
    }

    [HttpPost("facturas")]
    [RequirePermiso("Compras.Gestionar")]
    public async Task<IActionResult> CrearFactura([FromBody] CrearFacturaCompraRequest req, [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        return Respond(await _compras.CrearFacturaAsync(eid, req, _currentUser.Username, ct));
    }

    [HttpPost("facturas/{id:int}/anular")]
    [RequirePermiso("Compras.Gestionar")]
    public async Task<IActionResult> AnularFactura(int id, [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        return Respond(await _compras.AnularFacturaAsync(eid, id, _currentUser.Username, ct), "Factura anulada.");
    }

    // ── Pagos ──────────────────────────────────────────────────────────────────

    [HttpPost("pagos")]
    [RequirePermiso("Compras.Gestionar")]
    public async Task<IActionResult> RegistrarPago([FromBody] RegistrarPagoProveedorRequest req, [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        return Respond(await _compras.RegistrarPagoAsync(eid, req, _currentUser.Username, ct));
    }

    [HttpPost("pagos/{id:int}/anular")]
    [RequirePermiso("Compras.Gestionar")]
    public async Task<IActionResult> AnularPago(int id, [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        return Respond(await _compras.AnularPagoAsync(eid, id, _currentUser.Username, ct), "Pago anulado.");
    }

    [HttpGet("resumen")]
    [RequirePermiso("Compras.Ver")]
    public async Task<IActionResult> Resumen([FromQuery] int? empresaId, CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        return Respond(await _compras.ResumenAsync(eid, ct));
    }

    private int? Resolve(int? fromRequest) => _currentUser.EmpresaId ?? fromRequest;

    private object NoTenant() => ApiResponse.Fail(
        "No se pudo determinar la empresa. Si eres SuperAdmin, envía empresaId.",
        new[] { "AUTH_NO_TENANT" }, HttpContext.TraceIdentifier);
}
