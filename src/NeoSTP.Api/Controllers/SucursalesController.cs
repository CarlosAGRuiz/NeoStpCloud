using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NeoSTP.Api.Authorization;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Common;
using NeoSTP.Application.Empresas;
using NeoSTP.Application.Empresas.Dtos;

namespace NeoSTP.Api.Controllers;

[Authorize]
[Route("api/sucursales")]
public class SucursalesController : ApiControllerBase
{
    private readonly ISucursalesService _service;
    private readonly ICurrentUser _currentUser;

    public SucursalesController(ISucursalesService service, ICurrentUser currentUser)
    {
        _service = service;
        _currentUser = currentUser;
    }

    [HttpGet]
    [RequirePermiso("Core.Sucursales.Administrar")]
    public async Task<IActionResult> List([FromQuery] PagedQuery query, [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (ResolveEmpresaId(empresaId) is not int eid)
            return BadRequest(NoTenantResponse());
        return Respond(await _service.GetListAsync(eid, query, ct));
    }

    [HttpGet("{id:int}")]
    [RequirePermiso("Core.Sucursales.Administrar")]
    public async Task<IActionResult> Get(int id, [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (ResolveEmpresaId(empresaId) is not int eid)
            return BadRequest(NoTenantResponse());
        return Respond(await _service.GetByIdAsync(eid, id, ct));
    }

    [HttpPost]
    [RequirePermiso("Core.Sucursales.Administrar")]
    public async Task<IActionResult> Create([FromBody] CreateSucursalRequest request, CancellationToken ct)
    {
        if (ResolveEmpresaId(request.EmpresaId) is not int eid)
            return BadRequest(NoTenantResponse());
        return Respond(await _service.CreateAsync(eid, request, _currentUser.Username, ct));
    }

    [HttpPut("{id:int}")]
    [RequirePermiso("Core.Sucursales.Administrar")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateSucursalRequest request, [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (ResolveEmpresaId(empresaId) is not int eid)
            return BadRequest(NoTenantResponse());
        return Respond(await _service.UpdateAsync(eid, id, request, _currentUser.Username, ct));
    }

    [HttpPatch("{id:int}/inactivar")]
    [RequirePermiso("Core.Sucursales.Administrar")]
    public async Task<IActionResult> Inactivar(int id, [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (ResolveEmpresaId(empresaId) is not int eid)
            return BadRequest(NoTenantResponse());
        return Respond(await _service.InactivarAsync(eid, id, _currentUser.Username, ct), "Sucursal inactivada.");
    }

    private int? ResolveEmpresaId(int? fromRequest)
        => _currentUser.EmpresaId ?? fromRequest;

    private object NoTenantResponse() => Shared.ApiResponse.Fail(
        "No se pudo determinar la empresa. Si eres SuperAdmin, envía empresaId.",
        new[] { "AUTH_NO_TENANT" }, HttpContext.TraceIdentifier);
}
