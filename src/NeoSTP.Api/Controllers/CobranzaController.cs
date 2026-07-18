using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NeoSTP.Api.Authorization;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Cobranza;
using NeoSTP.Application.Cobranza.Dtos;
using NeoSTP.Shared;

namespace NeoSTP.Api.Controllers;

/// <summary>
/// Cobranza / cuentas por cobrar (B-2 NeoCloud Mobile). Saldos derivados de DTE a crédito,
/// registro de pagos y seguimiento. Lectura: Cobros.Ver; escritura: Cobros.Gestionar.
/// </summary>
[Authorize]
[Route("api/cobros")]
public class CobranzaController : ApiControllerBase
{
    private readonly ICobranzaService _service;
    private readonly ICobroQrService _qr;
    private readonly IRecordatorioCobroService _recordatorios;
    private readonly ICurrentUser _currentUser;

    public CobranzaController(ICobranzaService service, ICobroQrService qr, IRecordatorioCobroService recordatorios, ICurrentUser currentUser)
    {
        _service = service;
        _qr = qr;
        _recordatorios = recordatorios;
        _currentUser = currentUser;
    }

    /// <summary>Resumen de cartera para el dashboard (pendiente, vencido, clientes con deuda).</summary>
    [HttpGet("resumen")]
    [RequirePermiso("Cobros.Ver")]
    public async Task<IActionResult> Resumen([FromQuery] int? empresaId, CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        return Ok(ApiResponse<CobranzaResumenDto>.Ok(await _service.GetResumenAsync(eid, ct), traceId: HttpContext.TraceIdentifier));
    }

    /// <summary>Facturas con saldo pendiente (pendientes/vencidas), filtrables y paginadas.</summary>
    [HttpGet("pendientes")]
    [RequirePermiso("Cobros.Ver")]
    public async Task<IActionResult> Pendientes([FromQuery] CobranzaQuery query, [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        return Respond(await _service.GetPendientesAsync(eid, query, ct));
    }

    /// <summary>Saldo consolidado de un cliente + sus facturas pendientes.</summary>
    [HttpGet("clientes/{clienteId:int}")]
    [RequirePermiso("Cobros.Ver")]
    public async Task<IActionResult> SaldoCliente(int clienteId, [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        return Respond(await _service.GetSaldoClienteAsync(eid, clienteId, ct));
    }

    /// <summary>Historial de pagos registrados contra un DTE.</summary>
    [HttpGet("dte/{dteId:int}/pagos")]
    [RequirePermiso("Cobros.Ver")]
    public async Task<IActionResult> Pagos(int dteId, [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        return Respond(await _service.GetPagosAsync(eid, dteId, ct));
    }

    /// <summary>Registra un pago contra un DTE (factura/CCF a crédito).</summary>
    [HttpPost("dte/{dteId:int}/pagos")]
    [RequirePermiso("Cobros.Gestionar")]
    public async Task<IActionResult> RegistrarPago(int dteId, [FromBody] RegistrarPagoRequest req, [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        return Respond(await _service.RegistrarPagoAsync(eid, dteId, req, _currentUser.Username, ct));
    }

    /// <summary>Confirma un pago que estaba en revisión.</summary>
    [HttpPost("pagos/{pagoId:int}/confirmar")]
    [RequirePermiso("Cobros.Gestionar")]
    public async Task<IActionResult> Confirmar(int pagoId, [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        return Respond(await _service.ConfirmarPagoAsync(eid, pagoId, _currentUser.Username, ct), "Pago confirmado.");
    }

    /// <summary>Anula un pago (deja de contar para el saldo).</summary>
    [HttpPost("pagos/{pagoId:int}/anular")]
    [RequirePermiso("Cobros.Gestionar")]
    public async Task<IActionResult> Anular(int pagoId, [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        return Respond(await _service.AnularPagoAsync(eid, pagoId, _currentUser.Username, ct), "Pago anulado.");
    }

    // ─── QR / enlaces de cobro (B-5) ─────────────────────────────────────────

    /// <summary>Cuentas/pasarelas de cobro de la empresa.</summary>
    [HttpGet("cuentas")]
    [RequirePermiso("Cobros.Ver")]
    public async Task<IActionResult> ListarCuentas([FromQuery] int? empresaId, CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        return Ok(ApiResponse<IReadOnlyList<CuentaCobroDto>>.Ok(await _qr.ListarCuentasAsync(eid, ct), traceId: HttpContext.TraceIdentifier));
    }

    [HttpPost("cuentas")]
    [RequirePermiso("Cobros.Gestionar")]
    public async Task<IActionResult> CrearCuenta([FromBody] CrearCuentaCobroRequest req, [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        return Respond(await _qr.CrearCuentaAsync(eid, req, _currentUser.Username, ct));
    }

    [HttpPut("cuentas/{id:int}")]
    [RequirePermiso("Cobros.Gestionar")]
    public async Task<IActionResult> ActualizarCuenta(int id, [FromBody] CrearCuentaCobroRequest req, [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        return Respond(await _qr.ActualizarCuentaAsync(eid, id, req, _currentUser.Username, ct));
    }

    [HttpPost("cuentas/{id:int}/inactivar")]
    [RequirePermiso("Cobros.Gestionar")]
    public async Task<IActionResult> InactivarCuenta(int id, [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        return Respond(await _qr.InactivarCuentaAsync(eid, id, _currentUser.Username, ct), "Cuenta inactivada.");
    }

    /// <summary>Genera un QR de pago (asociado a una factura o a un monto) para compartir con el cliente.</summary>
    [HttpPost("qr")]
    [RequirePermiso("Cobros.Ver")]
    public async Task<IActionResult> GenerarQr([FromBody] GenerarQrCobroRequest req, [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        return Respond(await _qr.GenerarQrAsync(eid, req, ct));
    }

    /// <summary>Solicitud de cobro en PDF (branding + monto + cuenta + QR) para adjuntar o compartir.</summary>
    [HttpPost("pdf")]
    [RequirePermiso("Cobros.Ver")]
    public async Task<IActionResult> GenerarPdf([FromBody] GenerarQrCobroRequest req, [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        var result = await _qr.GenerarPdfAsync(eid, req, ct);
        if (result.IsFailure) return Respond(result);
        return File(result.Value!.Pdf, "application/pdf", result.Value.FileName);
    }

    /// <summary>Ejecuta recordatorios salientes de facturas vencidas (email/WhatsApp).</summary>
    [HttpPost("recordatorios/ejecutar")]
    [RequirePermiso("Cobros.Gestionar")]
    public async Task<IActionResult> EjecutarRecordatorios([FromBody] EjecutarRecordatoriosCobroRequest req, [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        return Respond(await _recordatorios.EjecutarAsync(eid, req, _currentUser.Username, ct));
    }

    /// <summary>Configuración de recordatorios automáticos de la empresa (reglas, canales, plantilla).</summary>
    [HttpGet("recordatorios/configuracion")]
    [RequirePermiso("Cobros.Ver")]
    public async Task<IActionResult> GetConfigRecordatorios([FromQuery] int? empresaId, CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        return Respond(await _recordatorios.GetConfiguracionAsync(eid, ct));
    }

    [HttpPut("recordatorios/configuracion")]
    [RequirePermiso("Cobros.Gestionar")]
    public async Task<IActionResult> GuardarConfigRecordatorios([FromBody] GuardarConfigRecordatorioRequest req, [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        return Respond(await _recordatorios.GuardarConfiguracionAsync(eid, req, _currentUser.Username, ct));
    }

    private int? Resolve(int? fromRequest) => _currentUser.EmpresaId ?? fromRequest;

    private object NoTenant() => Shared.ApiResponse.Fail(
        "No se pudo determinar la empresa. Si eres SuperAdmin, envía empresaId.",
        new[] { "AUTH_NO_TENANT" }, HttpContext.TraceIdentifier);
}
