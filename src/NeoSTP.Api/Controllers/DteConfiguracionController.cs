using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NeoSTP.Api.Authorization;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Dte;
using NeoSTP.Application.Dte.Dtos;

namespace NeoSTP.Api.Controllers;

[Authorize]
[Route("api/dte/configuracion")]
public class DteConfiguracionController : ApiControllerBase
{
    private readonly IDteConfiguracionService _service;
    private readonly ICurrentUser _currentUser;

    public DteConfiguracionController(IDteConfiguracionService service, ICurrentUser currentUser)
    {
        _service = service;
        _currentUser = currentUser;
    }

    [HttpGet]
    [RequirePermiso("DTE.Configurar")]
    public async Task<IActionResult> Get([FromQuery] int? empresaId, CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        return Respond(await _service.GetAsync(eid, ct));
    }

    [HttpPut]
    [RequirePermiso("DTE.Configurar")]
    public async Task<IActionResult> Save([FromBody] SaveDteConfiguracionRequest request, [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        return Respond(await _service.SaveAsync(eid, request, _currentUser.Username, ct));
    }

    [HttpPost("certificado")]
    [RequirePermiso("DTE.Configurar")]
    public async Task<IActionResult> UploadCertificado([FromBody] UploadCertificadoRequest request, [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        return Respond(await _service.UploadCertificadoAsync(eid, request, _currentUser.Username, ct));
    }

    [HttpDelete("certificado")]
    [RequirePermiso("DTE.Configurar")]
    public async Task<IActionResult> EliminarCertificado([FromQuery] int? empresaId, CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        return Respond(await _service.EliminarCertificadoAsync(eid, _currentUser.Username, ct), "Certificado eliminado.");
    }

    [HttpPost("probar-conexion")]
    [RequirePermiso("DTE.Configurar")]
    public async Task<IActionResult> ProbarConexion([FromQuery] int? empresaId, CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        return Respond(await _service.ProbarConexionAsync(eid, _currentUser.Username, ct));
    }

    private int? Resolve(int? fromRequest) => _currentUser.EmpresaId ?? fromRequest;

    private object NoTenant() => Shared.ApiResponse.Fail(
        "No se pudo determinar la empresa. Si eres SuperAdmin, envía empresaId.",
        new[] { "AUTH_NO_TENANT" }, HttpContext.TraceIdentifier);
}
