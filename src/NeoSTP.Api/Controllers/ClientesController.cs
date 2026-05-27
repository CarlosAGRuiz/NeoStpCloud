using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NeoSTP.Api.Authorization;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Clientes;
using NeoSTP.Application.Clientes.Dtos;
using NeoSTP.Application.Common;

namespace NeoSTP.Api.Controllers;

[Authorize]
[Route("api/clientes")]
public class ClientesController : ApiControllerBase
{
    private readonly IClientesService _service;
    private readonly ICurrentUser _currentUser;

    public ClientesController(IClientesService service, ICurrentUser currentUser)
    {
        _service = service;
        _currentUser = currentUser;
    }

    [HttpGet]
    [RequirePermiso("Clientes.Ver")]
    public async Task<IActionResult> List([FromQuery] PagedQuery query, [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        return Respond(await _service.GetListAsync(eid, query, ct));
    }

    [HttpGet("{id:int}")]
    [RequirePermiso("Clientes.Ver")]
    public async Task<IActionResult> Get(int id, [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        return Respond(await _service.GetByIdAsync(eid, id, ct));
    }

    [HttpPost]
    [RequirePermiso("Clientes.Crear")]
    public async Task<IActionResult> Create([FromBody] CreateClienteRequest request, [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        return Respond(await _service.CreateAsync(eid, request, _currentUser.Username, ct));
    }

    [HttpPut("{id:int}")]
    [RequirePermiso("Clientes.Editar")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateClienteRequest request, [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        return Respond(await _service.UpdateAsync(eid, id, request, _currentUser.Username, ct));
    }

    [HttpPatch("{id:int}/inactivar")]
    [RequirePermiso("Clientes.Editar")]
    public async Task<IActionResult> Inactivar(int id, [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        return Respond(await _service.InactivarAsync(eid, id, _currentUser.Username, ct), "Cliente inactivado.");
    }

    private int? Resolve(int? fromRequest) => _currentUser.EmpresaId ?? fromRequest;

    private object NoTenant() => Shared.ApiResponse.Fail(
        "No se pudo determinar la empresa. Si eres SuperAdmin, envía empresaId.",
        new[] { "AUTH_NO_TENANT" }, HttpContext.TraceIdentifier);
}
