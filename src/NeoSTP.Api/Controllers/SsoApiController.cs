using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NeoSTP.Api.Authorization;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Auth.Dtos;
using NeoSTP.Shared;

namespace NeoSTP.Api.Controllers;

/// <summary>
/// Configuración de SSO corporativo (OIDC) por empresa (E3). El flujo de login
/// federado es interactivo (web); esta API solo administra el mapeo dominio→empresa,
/// el auto-aprovisionamiento y el rol por defecto. Requiere Seguridad.Sso.Gestionar.
/// </summary>
[Authorize]
[Route("api/sso")]
public class SsoApiController : ApiControllerBase
{
    private readonly ISsoConfigService _sso;
    private readonly ICurrentUser _currentUser;

    public SsoApiController(ISsoConfigService sso, ICurrentUser currentUser)
    {
        _sso = sso;
        _currentUser = currentUser;
    }

    [HttpGet("config")]
    [RequirePermiso("Seguridad.Sso.Gestionar")]
    public async Task<IActionResult> GetConfig([FromQuery] int? empresaId, CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        return Respond(await _sso.GetAsync(eid, ct));
    }

    [HttpPut("config")]
    [RequirePermiso("Seguridad.Sso.Gestionar")]
    public async Task<IActionResult> GuardarConfig([FromBody] GuardarEmpresaSsoRequest req, [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        return Respond(await _sso.GuardarAsync(eid, req, _currentUser.Username, ct));
    }

    private int? Resolve(int? fromRequest) => _currentUser.EmpresaId ?? fromRequest;

    private object NoTenant() => ApiResponse.Fail(
        "No se pudo determinar la empresa. Si eres SuperAdmin, envía empresaId.",
        new[] { "AUTH_NO_TENANT" }, HttpContext.TraceIdentifier);
}
