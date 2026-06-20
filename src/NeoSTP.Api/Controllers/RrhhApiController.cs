using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NeoSTP.Api.Authorization;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Common;
using NeoSTP.Application.Rrhh;
using NeoSTP.Application.Rrhh.Dtos;
using NeoSTP.Shared;

namespace NeoSTP.Api.Controllers;

/// <summary>
/// NEORRHH — empleados y planilla (API-first, consumible por la app y la web). Requiere el
/// módulo NEORRHH activo. Empleados: Rrhh.Empleados.*; planilla: Rrhh.Nomina.*.
/// </summary>
[Authorize]
[RequireModule("NEORRHH")]
[Route("api/rrhh")]
public class RrhhApiController : ApiControllerBase
{
    private readonly IEmpleadosService _empleados;
    private readonly IPlanillaService _planilla;
    private readonly IPrestacionesRrhhService _prestaciones;
    private readonly INominaPdfService _pdf;
    private readonly ICurrentUser _currentUser;

    public RrhhApiController(
        IEmpleadosService empleados,
        IPlanillaService planilla,
        IPrestacionesRrhhService prestaciones,
        INominaPdfService pdf,
        ICurrentUser currentUser)
    {
        _empleados = empleados;
        _planilla = planilla;
        _prestaciones = prestaciones;
        _pdf = pdf;
        _currentUser = currentUser;
    }

    // Prestaciones laborales

    [HttpGet("prestaciones/politica")]
    [RequirePermiso("Rrhh.Nomina.Ver")]
    public async Task<IActionResult> GetPoliticaPrestaciones([FromQuery] int? empresaId, CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        return Respond(await _prestaciones.GetPoliticaAsync(eid, ct));
    }

    [HttpPut("prestaciones/politica")]
    [RequirePermiso("Rrhh.Nomina.Gestionar")]
    public async Task<IActionResult> UpdatePoliticaPrestaciones(
        [FromBody] UpdatePoliticaPrestacionesRequest request,
        [FromQuery] int? empresaId,
        CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        return Respond(await _prestaciones.UpdatePoliticaAsync(eid, request, _currentUser.Username, ct));
    }

    [HttpGet("vacaciones")]
    [RequirePermiso("Rrhh.Nomina.Ver")]
    public async Task<IActionResult> ListVacaciones(
        [FromQuery] PagedQuery query,
        [FromQuery] int? empleadoId,
        [FromQuery] string? estado,
        [FromQuery] int? empresaId,
        CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        return Respond(await _prestaciones.ListVacacionesAsync(eid, empleadoId, estado, query, ct));
    }

    [HttpGet("vacaciones/empleados/{empleadoId:int}/resumen")]
    [RequirePermiso("Rrhh.Nomina.Ver")]
    public async Task<IActionResult> GetVacacionResumen(
        int empleadoId,
        [FromQuery] DateOnly? fechaCorte,
        [FromQuery] int? empresaId,
        CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        return Respond(await _prestaciones.GetVacacionResumenAsync(eid, empleadoId, fechaCorte, ct));
    }

    [HttpPost("vacaciones")]
    [RequirePermiso("Rrhh.Nomina.Gestionar")]
    public async Task<IActionResult> SolicitarVacacion(
        [FromBody] CrearSolicitudVacacionRequest request,
        [FromQuery] int? empresaId,
        CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        return Respond(await _prestaciones.SolicitarVacacionAsync(eid, request, _currentUser.Username, ct));
    }

    [HttpPost("vacaciones/{id:int}/aprobar")]
    [RequirePermiso("Rrhh.Nomina.Gestionar")]
    public async Task<IActionResult> AprobarVacacion(
        int id,
        [FromBody] ResolverSolicitudVacacionRequest request,
        [FromQuery] int? empresaId,
        CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        return Respond(await _prestaciones.AprobarVacacionAsync(eid, id, request, _currentUser.Username, ct));
    }

    [HttpPost("vacaciones/{id:int}/rechazar")]
    [RequirePermiso("Rrhh.Nomina.Gestionar")]
    public async Task<IActionResult> RechazarVacacion(
        int id,
        [FromBody] ResolverSolicitudVacacionRequest request,
        [FromQuery] int? empresaId,
        CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        return Respond(await _prestaciones.RechazarVacacionAsync(eid, id, request, _currentUser.Username, ct));
    }

    [HttpPost("vacaciones/{id:int}/cancelar")]
    [RequirePermiso("Rrhh.Nomina.Gestionar")]
    public async Task<IActionResult> CancelarVacacion(int id, [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        return Respond(await _prestaciones.CancelarVacacionAsync(eid, id, _currentUser.Username, ct), "Vacación cancelada.");
    }

    [HttpGet("aguinaldos/{anio:int}")]
    [RequirePermiso("Rrhh.Nomina.Ver")]
    public async Task<IActionResult> ListAguinaldos(int anio, [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        return Respond(await _prestaciones.ListAguinaldosAsync(eid, anio, ct));
    }

    [HttpPost("aguinaldos/{anio:int}/calcular")]
    [RequirePermiso("Rrhh.Nomina.Gestionar")]
    public async Task<IActionResult> CalcularAguinaldos(int anio, [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        return Respond(await _prestaciones.CalcularAguinaldosAsync(eid, anio, _currentUser.Username, ct));
    }

    [HttpPost("aguinaldos/{anio:int}/aprobar")]
    [RequirePermiso("Rrhh.Nomina.Gestionar")]
    public async Task<IActionResult> AprobarAguinaldos(int anio, [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        return Respond(await _prestaciones.AprobarAguinaldosAsync(eid, anio, _currentUser.Username, ct));
    }

    // ── Empleados ────────────────────────────────────────────────────────────

    [HttpGet("empleados")]
    [RequirePermiso("Rrhh.Empleados.Ver")]
    public async Task<IActionResult> ListEmpleados([FromQuery] PagedQuery query, [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        return Respond(await _empleados.GetListAsync(eid, query, ct));
    }

    [HttpGet("empleados/{id:int}")]
    [RequirePermiso("Rrhh.Empleados.Ver")]
    public async Task<IActionResult> GetEmpleado(int id, [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        return Respond(await _empleados.GetAsync(eid, id, ct));
    }

    [HttpPost("empleados")]
    [RequirePermiso("Rrhh.Empleados.Gestionar")]
    public async Task<IActionResult> CrearEmpleado([FromBody] CreateEmpleadoRequest req, [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        return Respond(await _empleados.CreateAsync(eid, req, _currentUser.Username, ct));
    }

    [HttpPut("empleados/{id:int}")]
    [RequirePermiso("Rrhh.Empleados.Gestionar")]
    public async Task<IActionResult> EditarEmpleado(int id, [FromBody] UpdateEmpleadoRequest req, [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        return Respond(await _empleados.UpdateAsync(eid, id, req, _currentUser.Username, ct));
    }

    [HttpPost("empleados/{id:int}/inactivar")]
    [RequirePermiso("Rrhh.Empleados.Gestionar")]
    public async Task<IActionResult> InactivarEmpleado(int id, [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        return Respond(await _empleados.InactivarAsync(eid, id, _currentUser.Username, ct), "Empleado inactivado.");
    }

    // ── Planilla ─────────────────────────────────────────────────────────────

    [HttpGet("planillas")]
    [RequirePermiso("Rrhh.Nomina.Ver")]
    public async Task<IActionResult> ListPlanillas([FromQuery] PagedQuery query, [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        return Respond(await _planilla.ListAsync(eid, query, ct));
    }

    [HttpGet("planillas/{id:int}")]
    [RequirePermiso("Rrhh.Nomina.Ver")]
    public async Task<IActionResult> GetPlanilla(int id, [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        return Respond(await _planilla.GetAsync(eid, id, ct));
    }

    [HttpPost("planillas")]
    [RequirePermiso("Rrhh.Nomina.Gestionar")]
    public async Task<IActionResult> CrearPlanilla([FromBody] CrearPlanillaRequest req, [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        return Respond(await _planilla.CrearAsync(eid, req, _currentUser.Username, ct));
    }

    [HttpPost("planillas/{id:int}/cerrar")]
    [RequirePermiso("Rrhh.Nomina.Gestionar")]
    public async Task<IActionResult> CerrarPlanilla(int id, [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        return Respond(await _planilla.CerrarAsync(eid, id, _currentUser.Username, ct), "Planilla cerrada.");
    }

    [HttpPost("planillas/{id:int}/anular")]
    [RequirePermiso("Rrhh.Nomina.Gestionar")]
    public async Task<IActionResult> AnularPlanilla(int id, [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        return Respond(await _planilla.AnularAsync(eid, id, _currentUser.Username, ct), "Planilla anulada.");
    }

    /// <summary>Recibo de pago en PDF de un empleado en el período.</summary>
    [HttpGet("planillas/{id:int}/recibo/{empleadoId:int}")]
    [RequirePermiso("Rrhh.Nomina.Ver")]
    [Produces("application/pdf")]
    public async Task<IActionResult> Recibo(int id, int empleadoId, [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        var result = await _planilla.GetReciboAsync(eid, id, empleadoId, ct);
        if (result.IsFailure) return Respond(result);
        return File(_pdf.GenerarRecibo(result.Value!), "application/pdf", $"recibo_{result.Value!.EmpleadoCodigo}.pdf");
    }

    private int? Resolve(int? fromRequest) => _currentUser.EmpresaId ?? fromRequest;

    private object NoTenant() => ApiResponse.Fail(
        "No se pudo determinar la empresa. Si eres SuperAdmin, envía empresaId.",
        new[] { "AUTH_NO_TENANT" }, HttpContext.TraceIdentifier);
}
