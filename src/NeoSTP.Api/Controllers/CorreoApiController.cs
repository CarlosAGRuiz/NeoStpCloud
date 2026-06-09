using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NeoSTP.Api.Authorization;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Comunicaciones;
using NeoSTP.Shared;

namespace NeoSTP.Api.Controllers;

/// <summary>
/// Configuración de correo saliente (SMTP) por empresa (API-first, consumible por la app y la web).
/// La contraseña se cifra y nunca se devuelve. Permiso: Core.Correo.Configurar.
/// </summary>
[Authorize]
[Route("api/correo")]
public class CorreoApiController : ApiControllerBase
{
    private readonly IConfiguracionCorreoService _correo;
    private readonly ICurrentUser _currentUser;

    public CorreoApiController(IConfiguracionCorreoService correo, ICurrentUser currentUser)
    {
        _correo = correo;
        _currentUser = currentUser;
    }

    /// <summary>Configuración SMTP actual de la empresa (sin exponer la contraseña).</summary>
    [HttpGet]
    [RequirePermiso("Core.Correo.Configurar")]
    public async Task<IActionResult> Get([FromQuery] int? empresaId, CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        return Respond(await _correo.GetAsync(eid, ct));
    }

    /// <summary>Crea o actualiza la configuración SMTP de la empresa. Password vacío conserva el anterior.</summary>
    [HttpPut]
    [RequirePermiso("Core.Correo.Configurar")]
    public async Task<IActionResult> Guardar([FromBody] GuardarConfiguracionCorreoRequest req, [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        return Respond(await _correo.GuardarAsync(eid, req, _currentUser.Username, ct));
    }

    /// <summary>Envía un correo de prueba con la configuración actual de la empresa.</summary>
    [HttpPost("probar")]
    [RequirePermiso("Core.Correo.Configurar")]
    public async Task<IActionResult> Probar([FromBody] ProbarCorreoRequest req, [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        return Respond(await _correo.ProbarAsync(eid, req.Destino, _currentUser.Username, ct), $"Correo de prueba enviado a {req.Destino}.");
    }

    private int? Resolve(int? fromRequest) => _currentUser.EmpresaId ?? fromRequest;

    private object NoTenant() => ApiResponse.Fail(
        "No se pudo determinar la empresa. Si eres SuperAdmin, envía empresaId.",
        new[] { "AUTH_NO_TENANT" }, HttpContext.TraceIdentifier);
}
