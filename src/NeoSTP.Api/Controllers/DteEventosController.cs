using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NeoSTP.Api.Authorization;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Dte.Eventos;
using NeoSTP.Application.Dte.Eventos.Dtos;

namespace NeoSTP.Api.Controllers;

[Authorize]
[Route("api/dte/eventos")]
public class DteEventosController : ApiControllerBase
{
    private readonly IDteEventoService _service;
    private readonly IDteEventoPdfService _pdf;
    private readonly ICurrentUser _currentUser;

    public DteEventosController(IDteEventoService service, IDteEventoPdfService pdf, ICurrentUser currentUser)
    {
        _service = service;
        _pdf = pdf;
        _currentUser = currentUser;
    }

    // ----- Lectura -----

    [HttpGet]
    [RequirePermiso("DTE.Eventos.Ver")]
    public async Task<IActionResult> List([FromQuery] string? tipo, [FromQuery] string? estado, [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (ResolveEmpresaId(empresaId) is not int eid) return BadRequest(NoTenant());
        return Respond(await _service.GetListAsync(eid, tipo, estado, ct));
    }

    [HttpGet("{id:int}")]
    [RequirePermiso("DTE.Eventos.Ver")]
    public async Task<IActionResult> Get(int id, [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (ResolveEmpresaId(empresaId) is not int eid) return BadRequest(NoTenant());
        return Respond(await _service.GetByIdAsync(eid, id, ct));
    }

    [HttpGet("{id:int}/json")]
    [RequirePermiso("DTE.Eventos.Ver")]
    public async Task<IActionResult> Json(int id, [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (ResolveEmpresaId(empresaId) is not int eid) return BadRequest(NoTenant());
        return Respond(await _service.GetJsonAsync(eid, id, ct));
    }

    [HttpGet("{id:int}/pdf")]
    [RequirePermiso("DTE.Eventos.Ver")]
    public async Task<IActionResult> Pdf(int id, [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (ResolveEmpresaId(empresaId) is not int eid) return BadRequest(NoTenant());
        var result = await _pdf.GenerarAsync(eid, id, ct);
        if (result.IsFailure) return Respond(result);
        return File(result.Value!.Content, result.Value.ContentType, result.Value.FileName);
    }

    // ----- Creación (delega en la transmisión certificada que persiste el evento) -----

    [HttpPost("invalidacion")]
    [RequirePermiso("DTE.Invalidar")]
    public async Task<IActionResult> CrearInvalidacion([FromBody] CrearEventoInvalidacionRequest body, [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (ResolveEmpresaId(empresaId) is not int eid) return BadRequest(NoTenant());
        return Respond(await _service.CrearInvalidacionAsync(eid, body, _currentUser.Username, ct));
    }

    [HttpPost("contingencia")]
    [RequirePermiso("DTE.Contingencia")]
    public async Task<IActionResult> CrearContingencia([FromBody] CrearEventoContingenciaRequest body, [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (ResolveEmpresaId(empresaId) is not int eid) return BadRequest(NoTenant());
        return Respond(await _service.CrearContingenciaAsync(eid, body, _currentUser.Username, ct));
    }

    [HttpPost("retorno")]
    [RequirePermiso("DTE.Emitir")]
    public async Task<IActionResult> CrearRetorno([FromBody] CrearEventoRetornoRequest body, [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (ResolveEmpresaId(empresaId) is not int eid) return BadRequest(NoTenant());
        return Respond(await _service.CrearRetornoAsync(eid, body, _currentUser.Username, ct));
    }

    [HttpPost("operaciones-especiales")]
    [RequirePermiso("DTE.Emitir")]
    public async Task<IActionResult> CrearOperacionesEspeciales([FromBody] CrearEventoOperacionesEspecialesRequest body, [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (ResolveEmpresaId(empresaId) is not int eid) return BadRequest(NoTenant());
        return Respond(await _service.CrearOperacionesEspecialesAsync(eid, body, _currentUser.Username, ct));
    }

    private int? ResolveEmpresaId(int? fromRequest) => _currentUser.EmpresaId ?? fromRequest;

    private object NoTenant() => Shared.ApiResponse.Fail(
        "No se pudo determinar la empresa. SuperAdmin debe pasar empresaId.",
        new[] { "AUTH_NO_TENANT" }, HttpContext.TraceIdentifier);
}
