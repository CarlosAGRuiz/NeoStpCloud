using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NeoSTP.Api.Authorization;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Common;
using NeoSTP.Application.Pos;
using NeoSTP.Application.Pos.Dtos;
using NeoSTP.Shared;

namespace NeoSTP.Api.Controllers;

/// <summary>
/// NEOPOS — punto de venta (API-first, consumible por la app y la web). Requiere el módulo
/// NEOPOS activo. Ver: Pos.Ver; vender: Pos.Vender; anular: Pos.Anular.
/// </summary>
[Authorize]
[RequireModule("NEOPOS")]
[Route("api/pos")]
public class PosApiController : ApiControllerBase
{
    private readonly IPosService _pos;
    private readonly IPosConfigService _posConfig;
    private readonly IPosCajaService _caja;
    private readonly ITicketPdfService _ticketPdf;
    private readonly ICurrentUser _currentUser;

    public PosApiController(IPosService pos, IPosConfigService posConfig, IPosCajaService caja, ITicketPdfService ticketPdf, ICurrentUser currentUser)
    {
        _pos = pos;
        _posConfig = posConfig;
        _caja = caja;
        _ticketPdf = ticketPdf;
        _currentUser = currentUser;
    }

    [HttpGet("ventas")]
    [RequirePermiso("Pos.Ver")]
    public async Task<IActionResult> ListVentas([FromQuery] PagedQuery query, [FromQuery] DateOnly? desde, [FromQuery] DateOnly? hasta, [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        return Respond(await _pos.ListAsync(eid, desde, hasta, query, ct));
    }

    [HttpGet("ventas/{id:int}")]
    [RequirePermiso("Pos.Ver")]
    public async Task<IActionResult> GetVenta(int id, [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        return Respond(await _pos.GetAsync(eid, id, ct));
    }

    [HttpPost("ventas")]
    [RequirePermiso("Pos.Vender")]
    public async Task<IActionResult> CrearVenta([FromBody] CrearVentaRequest req, [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        return Respond(await _pos.CrearVentaAsync(eid, req, _currentUser.Username, ct));
    }

    [HttpPost("ventas/{id:int}/anular")]
    [RequirePermiso("Pos.Anular")]
    public async Task<IActionResult> AnularVenta(int id, [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        return Respond(await _pos.AnularAsync(eid, id, _currentUser.Username, ct), "Venta anulada.");
    }

    /// <summary>Promueve la venta a Factura/CCF electrónica (DTE).</summary>
    [HttpPost("ventas/{id:int}/promover")]
    [RequirePermiso("DTE.Emitir")]
    public async Task<IActionResult> PromoverVenta(int id, [FromBody] NeoSTP.Application.Pos.Dtos.PromoverVentaRequest req, [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        return Respond(await _pos.PromoverADteAsync(eid, id, req ?? new(), _currentUser.Username, ct));
    }

    /// <summary>Ticket de la venta en PDF (formato térmico).</summary>
    [HttpGet("ventas/{id:int}/ticket")]
    [RequirePermiso("Pos.Ver")]
    public async Task<IActionResult> Ticket(int id, [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        var t = await _pos.GetTicketAsync(eid, id, ct);
        if (t.IsFailure) return Respond(t);
        return File(_ticketPdf.GenerarTicket(t.Value!), "application/pdf", $"ticket_{t.Value!.Numero}.pdf");
    }

    [HttpPost("ventas/{id:int}/enviar")]
    [RequirePermiso("Pos.Ver")]
    public async Task<IActionResult> EnviarTicket(int id, [FromBody] EnviarTicketRequest req, [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        return Respond(await _pos.EnviarTicketCorreoAsync(eid, id, req.Email, _currentUser.Username, ct), "Ticket enviado.");
    }

    [HttpGet("resumen")]
    [RequirePermiso("Pos.Ver")]
    public async Task<IActionResult> Resumen([FromQuery] DateOnly? fecha, [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        return Respond(await _pos.ResumenDiaAsync(eid, fecha ?? DateOnly.FromDateTime(DateTime.UtcNow), ct));
    }

    // ── Impresoras ────────────────────────────────────────────────────────────

    [HttpGet("impresoras")]
    [RequirePermiso("Pos.Ver")]
    public async Task<IActionResult> ListImpresoras([FromQuery] int? empresaId, CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        return Respond(await _posConfig.ListImpresorasAsync(eid, ct));
    }

    [HttpPost("impresoras")]
    [RequirePermiso("Pos.Configurar")]
    public async Task<IActionResult> GuardarImpresora([FromBody] NeoSTP.Application.Pos.Dtos.GuardarImpresoraRequest req, [FromQuery] int? id, [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        return Respond(await _posConfig.GuardarImpresoraAsync(eid, id, req, _currentUser.Username, ct));
    }

    [HttpDelete("impresoras/{id:int}")]
    [RequirePermiso("Pos.Configurar")]
    public async Task<IActionResult> EliminarImpresora(int id, [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        return Respond(await _posConfig.EliminarImpresoraAsync(eid, id, _currentUser.Username, ct), "Impresora eliminada.");
    }

    /// <summary>Imprime el ticket de una venta enviando ESC/POS a una impresora de red.</summary>
    [HttpPost("ventas/{ventaId:int}/imprimir-red/{impresoraId:int}")]
    [RequirePermiso("Pos.Ver")]
    public async Task<IActionResult> ImprimirRed(int ventaId, int impresoraId, [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        return Respond(await _posConfig.ImprimirVentaEnRedAsync(eid, ventaId, impresoraId, _currentUser.Username, ct), "Ticket enviado a la impresora.");
    }

    [HttpPost("impresoras/{impresoraId:int}/probar")]
    [RequirePermiso("Pos.Configurar")]
    public async Task<IActionResult> ProbarImpresora(int impresoraId, [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        return Respond(await _posConfig.ProbarImpresoraAsync(eid, impresoraId, _currentUser.Username, ct), "Ticket de prueba enviado.");
    }

    // ── Caja (corte) ──────────────────────────────────────────────────────────

    /// <summary>Sesión de caja abierta con totales en vivo, o null si no hay ninguna.</summary>
    [HttpGet("caja/estado")]
    [RequirePermiso("Pos.Ver")]
    public async Task<IActionResult> EstadoCaja([FromQuery] int? empresaId, CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        return Respond(await _caja.GetEstadoAsync(eid, ct));
    }

    [HttpGet("caja")]
    [RequirePermiso("Pos.Ver")]
    public async Task<IActionResult> ListCajas([FromQuery] PagedQuery query, [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        return Respond(await _caja.ListAsync(eid, query, ct));
    }

    [HttpGet("caja/{id:int}")]
    [RequirePermiso("Pos.Ver")]
    public async Task<IActionResult> GetCaja(int id, [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        return Respond(await _caja.GetAsync(eid, id, ct));
    }

    [HttpPost("caja/abrir")]
    [RequirePermiso("Pos.Vender")]
    public async Task<IActionResult> AbrirCaja([FromBody] AbrirCajaRequest req, [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        return Respond(await _caja.AbrirAsync(eid, req, _currentUser.Username, ct));
    }

    [HttpPost("caja/{id:int}/cerrar")]
    [RequirePermiso("Pos.Vender")]
    public async Task<IActionResult> CerrarCaja(int id, [FromBody] CerrarCajaRequest req, [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        return Respond(await _caja.CerrarAsync(eid, id, req, _currentUser.Username, ct));
    }

    private int? Resolve(int? fromRequest) => _currentUser.EmpresaId ?? fromRequest;

    private object NoTenant() => ApiResponse.Fail(
        "No se pudo determinar la empresa. Si eres SuperAdmin, envía empresaId.",
        new[] { "AUTH_NO_TENANT" }, HttpContext.TraceIdentifier);
}
