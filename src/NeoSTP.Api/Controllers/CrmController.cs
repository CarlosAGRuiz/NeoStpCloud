using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NeoSTP.Api.Authorization;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Common;
using NeoSTP.Application.Crm;
using NeoSTP.Application.Crm.Dtos;
using NeoSTP.Shared;

namespace NeoSTP.Api.Controllers;

[Authorize]
[RequireModule("NEOCRM")]
[Route("api/crm")]
public class CrmController : ApiControllerBase
{
    private readonly ICrmService _crm;
    private readonly ICurrentUser _currentUser;

    public CrmController(ICrmService crm, ICurrentUser currentUser)
    {
        _crm = crm;
        _currentUser = currentUser;
    }

    [HttpGet("resumen")]
    [RequirePermiso("Crm.Oportunidades.Ver")]
    public async Task<IActionResult> Resumen([FromQuery] int? empresaId, CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        return Respond(await _crm.ResumenAsync(eid, ct));
    }

    [HttpGet("contactos")]
    [RequirePermiso("Crm.Contactos.Ver")]
    public async Task<IActionResult> ListContactos([FromQuery] PagedQuery query, [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        return Respond(await _crm.ListContactosAsync(eid, query, ct));
    }

    [HttpGet("contactos/{id:int}")]
    [RequirePermiso("Crm.Contactos.Ver")]
    public async Task<IActionResult> GetContacto(int id, [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        return Respond(await _crm.GetContactoAsync(eid, id, ct));
    }

    [HttpPost("contactos")]
    [RequirePermiso("Crm.Contactos.Gestionar")]
    public async Task<IActionResult> CrearContacto([FromBody] UpsertContactoCrmRequest req, [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        return Respond(await _crm.CrearContactoAsync(eid, req, _currentUser.Username, ct));
    }

    [HttpPut("contactos/{id:int}")]
    [RequirePermiso("Crm.Contactos.Gestionar")]
    public async Task<IActionResult> ActualizarContacto(int id, [FromBody] UpsertContactoCrmRequest req, [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        return Respond(await _crm.ActualizarContactoAsync(eid, id, req, _currentUser.Username, ct));
    }

    [HttpPost("contactos/{id:int}/inactivar")]
    [RequirePermiso("Crm.Contactos.Gestionar")]
    public async Task<IActionResult> InactivarContacto(int id, [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        return Respond(await _crm.InactivarContactoAsync(eid, id, _currentUser.Username, ct), "Contacto inactivado.");
    }

    [HttpGet("etapas")]
    [RequirePermiso("Crm.Oportunidades.Ver")]
    public async Task<IActionResult> ListEtapas([FromQuery] int? empresaId, CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        return Respond(await _crm.ListEtapasAsync(eid, ct));
    }

    [HttpPost("etapas")]
    [RequirePermiso("Crm.Oportunidades.Gestionar")]
    public async Task<IActionResult> CrearEtapa([FromBody] UpsertEtapaPipelineCrmRequest req, [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        return Respond(await _crm.CrearEtapaAsync(eid, req, _currentUser.Username, ct));
    }

    [HttpPut("etapas/{id:int}")]
    [RequirePermiso("Crm.Oportunidades.Gestionar")]
    public async Task<IActionResult> ActualizarEtapa(int id, [FromBody] UpsertEtapaPipelineCrmRequest req, [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        return Respond(await _crm.ActualizarEtapaAsync(eid, id, req, _currentUser.Username, ct));
    }

    [HttpGet("oportunidades")]
    [RequirePermiso("Crm.Oportunidades.Ver")]
    public async Task<IActionResult> ListOportunidades([FromQuery] PagedQuery query, [FromQuery] string? estado, [FromQuery] int? etapaId, [FromQuery] int? clienteId, [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        return Respond(await _crm.ListOportunidadesAsync(eid, estado, etapaId, clienteId, query, ct));
    }

    [HttpGet("oportunidades/{id:int}")]
    [RequirePermiso("Crm.Oportunidades.Ver")]
    public async Task<IActionResult> GetOportunidad(int id, [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        return Respond(await _crm.GetOportunidadAsync(eid, id, ct));
    }

    [HttpPost("oportunidades")]
    [RequirePermiso("Crm.Oportunidades.Gestionar")]
    public async Task<IActionResult> CrearOportunidad([FromBody] CrearOportunidadCrmRequest req, [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        return Respond(await _crm.CrearOportunidadAsync(eid, req, _currentUser.Username, ct));
    }

    [HttpPut("oportunidades/{id:int}")]
    [RequirePermiso("Crm.Oportunidades.Gestionar")]
    public async Task<IActionResult> ActualizarOportunidad(int id, [FromBody] ActualizarOportunidadCrmRequest req, [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        return Respond(await _crm.ActualizarOportunidadAsync(eid, id, req, _currentUser.Username, ct));
    }

    [HttpPost("oportunidades/{id:int}/etapa")]
    [RequirePermiso("Crm.Oportunidades.Gestionar")]
    public async Task<IActionResult> CambiarEtapa(int id, [FromBody] CambiarEtapaOportunidadRequest req, [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        return Respond(await _crm.CambiarEtapaAsync(eid, id, req, _currentUser.Username, ct));
    }

    [HttpGet("actividades")]
    [RequirePermiso("Crm.Actividades.Ver")]
    public async Task<IActionResult> ListActividades([FromQuery] PagedQuery query, [FromQuery] bool soloPendientes, [FromQuery] int? oportunidadId, [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        return Respond(await _crm.ListActividadesAsync(eid, soloPendientes, oportunidadId, query, ct));
    }

    [HttpPost("actividades")]
    [RequirePermiso("Crm.Actividades.Gestionar")]
    public async Task<IActionResult> CrearActividad([FromBody] CrearActividadCrmRequest req, [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        return Respond(await _crm.CrearActividadAsync(eid, req, _currentUser.Username, ct));
    }

    [HttpPost("actividades/{id:int}/completar")]
    [RequirePermiso("Crm.Actividades.Gestionar")]
    public async Task<IActionResult> CompletarActividad(int id, [FromBody] CompletarActividadCrmRequest req, [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        return Respond(await _crm.CompletarActividadAsync(eid, id, req, _currentUser.Username, ct));
    }

    [HttpPost("actividades/{id:int}/cancelar")]
    [RequirePermiso("Crm.Actividades.Gestionar")]
    public async Task<IActionResult> CancelarActividad(int id, [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        return Respond(await _crm.CancelarActividadAsync(eid, id, _currentUser.Username, ct), "Actividad cancelada.");
    }

    // ── Cotizaciones ──────────────────────────────────────────────────────────

    [HttpGet("cotizaciones")]
    [RequirePermiso("Crm.Cotizaciones.Ver")]
    public async Task<IActionResult> ListCotizaciones([FromQuery] PagedQuery query, [FromQuery] string? estado, [FromQuery] int? oportunidadId, [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        return Respond(await _crm.ListCotizacionesAsync(eid, estado, oportunidadId, query, ct));
    }

    [HttpGet("cotizaciones/{id:int}")]
    [RequirePermiso("Crm.Cotizaciones.Ver")]
    public async Task<IActionResult> GetCotizacion(int id, [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        return Respond(await _crm.GetCotizacionAsync(eid, id, ct));
    }

    [HttpPost("cotizaciones")]
    [RequirePermiso("Crm.Cotizaciones.Gestionar")]
    public async Task<IActionResult> CrearCotizacion([FromBody] CrearCotizacionCrmRequest req, [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        return Respond(await _crm.CrearCotizacionAsync(eid, req, _currentUser.Username, ct));
    }

    [HttpPost("cotizaciones/{id:int}/estado")]
    [RequirePermiso("Crm.Cotizaciones.Gestionar")]
    public async Task<IActionResult> CambiarEstadoCotizacion(int id, [FromBody] CambiarEstadoCotizacionRequest req, [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        return Respond(await _crm.CambiarEstadoCotizacionAsync(eid, id, req, _currentUser.Username, ct));
    }

    /// <summary>Convierte la cotización en Factura/CCF electrónica (emite el DTE).</summary>
    [HttpPost("cotizaciones/{id:int}/convertir-dte")]
    [RequirePermiso("DTE.Emitir")]
    public async Task<IActionResult> ConvertirCotizacion(int id, [FromBody] ConvertirCotizacionRequest? req, [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        return Respond(await _crm.ConvertirCotizacionADteAsync(eid, id, req ?? new(), _currentUser.Username, ct));
    }

    private int? Resolve(int? fromRequest) => _currentUser.EmpresaId ?? fromRequest;

    private object NoTenant() => ApiResponse.Fail(
        "No se pudo determinar la empresa. Si eres SuperAdmin, envia empresaId.",
        new[] { "AUTH_NO_TENANT" }, HttpContext.TraceIdentifier);
}
