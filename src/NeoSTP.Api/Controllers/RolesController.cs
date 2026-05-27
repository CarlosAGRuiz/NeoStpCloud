using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NeoSTP.Api.Authorization;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Roles;
using NeoSTP.Application.Roles.Dtos;

namespace NeoSTP.Api.Controllers;

[Authorize]
[Route("api/roles")]
public class RolesController : ApiControllerBase
{
    private readonly IRolesService _service;
    private readonly ICurrentUser _currentUser;

    public RolesController(IRolesService service, ICurrentUser currentUser)
    {
        _service = service;
        _currentUser = currentUser;
    }

    [HttpGet]
    [RequirePermiso("Core.Roles.Administrar")]
    public async Task<IActionResult> List(CancellationToken ct)
        => Respond(await _service.GetListAsync(_currentUser.EmpresaId, ct));

    [HttpGet("{id:int}")]
    [RequirePermiso("Core.Roles.Administrar")]
    public async Task<IActionResult> Get(int id, CancellationToken ct)
        => Respond(await _service.GetByIdAsync(_currentUser.EmpresaId, id, ct));

    [HttpPost]
    [RequirePermiso("Core.Roles.Administrar")]
    public async Task<IActionResult> Create([FromBody] CreateRolRequest request, CancellationToken ct)
        => Respond(await _service.CreateAsync(_currentUser.EmpresaId, request, _currentUser.Username, ct));

    [HttpPut("{id:int}")]
    [RequirePermiso("Core.Roles.Administrar")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateRolRequest request, CancellationToken ct)
        => Respond(await _service.UpdateAsync(_currentUser.EmpresaId, id, request, _currentUser.Username, ct));

    [HttpGet("permisos")]
    [RequirePermiso("Core.Roles.Administrar")]
    public async Task<IActionResult> Permisos(CancellationToken ct)
        => Respond(await _service.GetPermisosAsync(ct));
}
