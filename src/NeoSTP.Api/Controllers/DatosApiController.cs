using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NeoSTP.Api.Authorization;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Datos;
using NeoSTP.Shared;

namespace NeoSTP.Api.Controllers;

/// <summary>
/// Portabilidad de datos (E8): descarga todos los datos de la empresa como ZIP de CSVs.
/// </summary>
[Authorize]
[Route("api/datos")]
public class DatosApiController : ApiControllerBase
{
    private readonly IPortabilidadService _portabilidad;
    private readonly ICurrentUser _currentUser;

    public DatosApiController(IPortabilidadService portabilidad, ICurrentUser currentUser)
    {
        _portabilidad = portabilidad;
        _currentUser = currentUser;
    }

    [HttpGet("exportar")]
    [RequirePermiso("Datos.Exportar")]
    [Produces("application/zip")]
    public async Task<IActionResult> Exportar([FromQuery] int? empresaId, CancellationToken ct)
    {
        if ((_currentUser.EmpresaId ?? empresaId) is not int eid)
            return BadRequest(ApiResponse.Fail(
                "No se pudo determinar la empresa. Si eres SuperAdmin, envía empresaId.",
                new[] { "AUTH_NO_TENANT" }, HttpContext.TraceIdentifier));

        var r = await _portabilidad.ExportarAsync(eid, _currentUser.Username, ct);
        if (r.IsFailure) return Respond(r);
        return File(r.Value!.Contenido, "application/zip", r.Value.NombreArchivo);
    }
}
