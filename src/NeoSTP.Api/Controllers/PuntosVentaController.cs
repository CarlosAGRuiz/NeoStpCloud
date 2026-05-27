using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NeoSTP.Api.Authorization;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Common;
using NeoSTP.Application.Empresas;
using NeoSTP.Application.Empresas.Dtos;

namespace NeoSTP.Api.Controllers;

[Authorize]
[Route("api/puntos-venta")]
public class PuntosVentaController : ApiControllerBase
{
    private readonly IPuntosVentaService _service;
    private readonly ICurrentUser _currentUser;

    public PuntosVentaController(IPuntosVentaService service, ICurrentUser currentUser)
    {
        _service = service;
        _currentUser = currentUser;
    }

    [HttpGet]
    [RequirePermiso("Core.PuntosVenta.Administrar")]
    public async Task<IActionResult> List([FromQuery] PagedQuery query, [FromQuery] int? empresaId, [FromQuery] int? sucursalId, CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenantResponse());
        return Respond(await _service.GetListAsync(eid, sucursalId, query, ct));
    }

    [HttpGet("{id:int}")]
    [RequirePermiso("Core.PuntosVenta.Administrar")]
    public async Task<IActionResult> Get(int id, [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenantResponse());
        return Respond(await _service.GetByIdAsync(eid, id, ct));
    }

    [HttpPost]
    [RequirePermiso("Core.PuntosVenta.Administrar")]
    public async Task<IActionResult> Create([FromBody] CreatePuntoVentaRequest request, [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenantResponse());
        return Respond(await _service.CreateAsync(eid, request, _currentUser.Username, ct));
    }

    [HttpPut("{id:int}")]
    [RequirePermiso("Core.PuntosVenta.Administrar")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdatePuntoVentaRequest request, [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenantResponse());
        return Respond(await _service.UpdateAsync(eid, id, request, _currentUser.Username, ct));
    }

    [HttpPatch("{id:int}/inactivar")]
    [RequirePermiso("Core.PuntosVenta.Administrar")]
    public async Task<IActionResult> Inactivar(int id, [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenantResponse());
        return Respond(await _service.InactivarAsync(eid, id, _currentUser.Username, ct), "Punto de venta inactivado.");
    }

    private int? Resolve(int? fromRequest) => _currentUser.EmpresaId ?? fromRequest;

    private object NoTenantResponse() => Shared.ApiResponse.Fail(
        "No se pudo determinar la empresa. Si eres SuperAdmin, envía empresaId.",
        new[] { "AUTH_NO_TENANT" }, HttpContext.TraceIdentifier);
}
