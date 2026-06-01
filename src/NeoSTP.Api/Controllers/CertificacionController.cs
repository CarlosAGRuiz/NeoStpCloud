using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NeoSTP.Api.Authorization;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Dte.Certificacion;
using NeoSTP.Application.Dte.Certificacion.Dtos;

namespace NeoSTP.Api.Controllers;

[Authorize]
[Route("api/certificacion")]
public class CertificacionController : ApiControllerBase
{
    private readonly ICertificacionDteService _service;
    private readonly ICurrentUser _currentUser;

    public CertificacionController(ICertificacionDteService service, ICurrentUser currentUser)
    {
        _service = service;
        _currentUser = currentUser;
    }

    // ----- Lectura -----

    [HttpGet("resumen")]
    [RequirePermiso("Core.Certificacion.Ver")]
    public async Task<IActionResult> Resumen([FromQuery] int? empresaId, CancellationToken ct)
    {
        if (ResolveEmpresaId(empresaId) is not int eid) return BadRequest(NoTenant());
        return Respond(await _service.GetResumenAsync(eid, ct));
    }

    [HttpGet("matriz")]
    [RequirePermiso("Core.Certificacion.Ver")]
    public async Task<IActionResult> Matriz([FromQuery] int? empresaId, CancellationToken ct)
    {
        if (ResolveEmpresaId(empresaId) is not int eid) return BadRequest(NoTenant());
        return Respond(await _service.GetMatrizAsync(eid, ct));
    }

    [HttpGet("tipos/{codigo}/escenarios")]
    [RequirePermiso("Core.Certificacion.Ver")]
    public async Task<IActionResult> Escenarios(string codigo, [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (ResolveEmpresaId(empresaId) is not int eid) return BadRequest(NoTenant());
        return Respond(await _service.GetEscenariosAsync(codigo, eid, ct));
    }

    [HttpGet("errores")]
    [RequirePermiso("Core.Certificacion.Ver")]
    public async Task<IActionResult> Errores([FromQuery] string? codigoMh, [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (ResolveEmpresaId(empresaId) is not int eid) return BadRequest(NoTenant());
        return Respond(await _service.GetErroresAsync(eid, codigoMh, ct));
    }

    // ----- Operación -----

    [HttpPost("tipos/{codigo}/generar-prueba")]
    [RequirePermiso("Core.Certificacion.Operar")]
    public async Task<IActionResult> GenerarPrueba(string codigo, [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (ResolveEmpresaId(empresaId) is not int eid) return BadRequest(NoTenant());
        return Respond(await _service.GenerarPruebaAsync(codigo, eid, _currentUser.Username, ct));
    }

    [HttpPost("documentos/{id:int}/marcar-completado")]
    [RequirePermiso("Core.Certificacion.Operar")]
    public async Task<IActionResult> MarcarCompletado(int id, [FromBody] MarcarCompletadoRequest request, [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (ResolveEmpresaId(empresaId) is not int eid) return BadRequest(NoTenant());
        return Respond(await _service.MarcarCompletadoAsync(id, request, eid, _currentUser.Username, ct));
    }

    [HttpPost("documentos/{id:int}/reintentar")]
    [RequirePermiso("Core.Certificacion.Operar")]
    public async Task<IActionResult> Reintentar(int id, [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (ResolveEmpresaId(empresaId) is not int eid) return BadRequest(NoTenant());
        return Respond(await _service.ReintentarAsync(id, eid, _currentUser.Username, ct));
    }

    private int? ResolveEmpresaId(int? fromRequest)
        => _currentUser.EmpresaId ?? fromRequest;

    private object NoTenant() => Shared.ApiResponse.Fail(
        "No se pudo determinar la empresa. SuperAdmin debe pasar empresaId.",
        new[] { "AUTH_NO_TENANT" }, HttpContext.TraceIdentifier);
}
