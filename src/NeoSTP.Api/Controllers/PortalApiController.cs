using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NeoSTP.Api.Authorization;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Common;
using NeoSTP.Application.Portal;
using NeoSTP.Shared;

namespace NeoSTP.Api.Controllers;

/// <summary>
/// NEOPORTAL — gestión interna de enlaces públicos del portal de receptor.
/// Requiere el módulo NEOPORTAL activo. El portal público (resolución del token)
/// vive en la Web (<c>/portal/{token}</c>), no aquí.
/// </summary>
[Authorize]
[RequireModule("NEOPORTAL")]
[Route("api/portal")]
public class PortalApiController : ApiControllerBase
{
    private readonly IPortalService _portal;
    private readonly ICurrentUser _currentUser;

    public PortalApiController(IPortalService portal, ICurrentUser currentUser)
    {
        _portal = portal;
        _currentUser = currentUser;
    }

    [HttpGet("enlaces")]
    [RequirePermiso("Portal.Enlaces.Ver")]
    public async Task<IActionResult> ListEnlaces([FromQuery] PagedQuery query, [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        return Respond(await _portal.ListEnlacesAsync(eid, query, ct));
    }

    /// <summary>Genera un enlace público para un DTE. El token solo se devuelve en esta respuesta.</summary>
    [HttpPost("enlaces/documento/{dteDocumentoId:int}")]
    [RequirePermiso("Portal.Enlaces.Gestionar")]
    public async Task<IActionResult> GenerarEnlaceDocumento(int dteDocumentoId, [FromBody] GenerarEnlacePortalRequest? req, [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        return Respond(await _portal.GenerarEnlaceDocumentoAsync(eid, dteDocumentoId, req ?? new(), _currentUser.Username, ct));
    }

    /// <summary>Genera un enlace público de estado de cuenta para un cliente.</summary>
    [HttpPost("enlaces/estado-cuenta/{clienteId:int}")]
    [RequirePermiso("Portal.Enlaces.Gestionar")]
    public async Task<IActionResult> GenerarEnlaceEstadoCuenta(int clienteId, [FromBody] GenerarEnlacePortalRequest? req, [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        return Respond(await _portal.GenerarEnlaceEstadoCuentaAsync(eid, clienteId, req ?? new(), _currentUser.Username, ct));
    }

    [HttpPost("enlaces/{id:int}/revocar")]
    [RequirePermiso("Portal.Enlaces.Gestionar")]
    public async Task<IActionResult> Revocar(int id, [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        return Respond(await _portal.RevocarAsync(eid, id, _currentUser.Username, ct), "Enlace revocado.");
    }

    private int? Resolve(int? fromRequest) => _currentUser.EmpresaId ?? fromRequest;

    private object NoTenant() => ApiResponse.Fail(
        "No se pudo determinar la empresa. Si eres SuperAdmin, envía empresaId.",
        new[] { "AUTH_NO_TENANT" }, HttpContext.TraceIdentifier);
}
