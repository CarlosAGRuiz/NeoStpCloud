using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Notificaciones;
using NeoSTP.Application.Notificaciones.Dtos;
using NeoSTP.Shared;

namespace NeoSTP.Api.Controllers;

/// <summary>
/// Centro de alertas y notificaciones push (B-4). Alertas por empresa/usuario, registro de
/// dispositivos (FCM) y preferencias. Cualquier usuario de empresa autenticado gestiona las suyas.
/// </summary>
[Authorize]
[Route("api/alertas")]
public class AlertasController : ApiControllerBase
{
    private readonly IAlertaService _alertas;
    private readonly IAlertaGeneracionService _generacion;
    private readonly ICurrentUser _currentUser;

    public AlertasController(IAlertaService alertas, IAlertaGeneracionService generacion, ICurrentUser currentUser)
    {
        _alertas = alertas;
        _generacion = generacion;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] AlertaQuery query, [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (!Ctx(empresaId, out var eid, out var uid, out var err)) return err!;
        return Respond(await _alertas.ListarAsync(eid, uid, query, ct));
    }

    [HttpGet("resumen")]
    public async Task<IActionResult> Resumen([FromQuery] int? empresaId, CancellationToken ct)
    {
        if (!Ctx(empresaId, out var eid, out var uid, out var err)) return err!;
        return Ok(ApiResponse<AlertaResumenDto>.Ok(await _alertas.ResumenAsync(eid, uid, ct), traceId: HttpContext.TraceIdentifier));
    }

    [HttpPost("{id:int}/leer")]
    public async Task<IActionResult> Leer(int id, [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (!Ctx(empresaId, out var eid, out var uid, out var err)) return err!;
        return Respond(await _alertas.MarcarLeidaAsync(eid, uid, id, ct), "Alerta marcada como leída.");
    }

    [HttpPost("{id:int}/resolver")]
    public async Task<IActionResult> Resolver(int id, [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (!Ctx(empresaId, out var eid, out var uid, out var err)) return err!;
        return Respond(await _alertas.ResolverAsync(eid, uid, id, ct), "Alerta resuelta.");
    }

    [HttpPost("leer-todas")]
    public async Task<IActionResult> LeerTodas([FromQuery] int? empresaId, CancellationToken ct)
    {
        if (!Ctx(empresaId, out var eid, out var uid, out var err)) return err!;
        return Respond(await _alertas.MarcarTodasLeidasAsync(eid, uid, ct), "Alertas marcadas como leídas.");
    }

    /// <summary>Recalcula las alertas de la empresa desde datos reales (DTE rechazado, cert por vencer, facturas vencidas).</summary>
    [HttpPost("generar")]
    public async Task<IActionResult> Generar([FromQuery] int? empresaId, CancellationToken ct)
    {
        if (!Ctx(empresaId, out var eid, out _, out var err)) return err!;
        var creadas = await _generacion.GenerarAsync(eid, ct);
        return Ok(ApiResponse<object>.Ok(new { creadas }, traceId: HttpContext.TraceIdentifier));
    }

    // ── Dispositivos ──
    [HttpPost("dispositivos")]
    public async Task<IActionResult> RegistrarDispositivo([FromBody] RegistrarDispositivoRequest req, [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (!Ctx(empresaId, out var eid, out var uid, out var err)) return err!;
        return Respond(await _alertas.RegistrarDispositivoAsync(eid, uid, req, ct), "Dispositivo registrado.");
    }

    [HttpPost("dispositivos/eliminar")]
    public async Task<IActionResult> EliminarDispositivo([FromBody] RegistrarDispositivoRequest req, [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (!Ctx(empresaId, out var eid, out var uid, out var err)) return err!;
        return Respond(await _alertas.EliminarDispositivoAsync(eid, uid, req.Token ?? string.Empty, ct), "Dispositivo eliminado.");
    }

    // ── Preferencias ──
    [HttpGet("preferencias")]
    public async Task<IActionResult> GetPreferencias([FromQuery] int? empresaId, CancellationToken ct)
    {
        if (!Ctx(empresaId, out var eid, out var uid, out var err)) return err!;
        return Ok(ApiResponse<PreferenciaNotificacionDto>.Ok(await _alertas.GetPreferenciasAsync(eid, uid, ct), traceId: HttpContext.TraceIdentifier));
    }

    [HttpPut("preferencias")]
    public async Task<IActionResult> GuardarPreferencias([FromBody] PreferenciaNotificacionDto req, [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (!Ctx(empresaId, out var eid, out var uid, out var err)) return err!;
        return Respond(await _alertas.GuardarPreferenciasAsync(eid, uid, req, ct), "Preferencias guardadas.");
    }

    // ── Helper: resuelve empresa (token) + usuario ──
    private bool Ctx(int? fromRequest, out int empresaId, out int usuarioId, out IActionResult? error)
    {
        empresaId = 0; usuarioId = 0; error = null;
        if ((_currentUser.EmpresaId ?? fromRequest) is not int eid)
        {
            error = BadRequest(Shared.ApiResponse.Fail("No se pudo determinar la empresa.", new[] { "AUTH_NO_TENANT" }, HttpContext.TraceIdentifier));
            return false;
        }
        if (_currentUser.UserId is not int uid)
        {
            error = Unauthorized(Shared.ApiResponse.Fail("No autenticado.", new[] { "AUTH_INVALID" }, HttpContext.TraceIdentifier));
            return false;
        }
        empresaId = eid; usuarioId = uid;
        return true;
    }
}
