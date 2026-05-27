using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NeoSTP.Api.Authorization;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Common;
using NeoSTP.Application.Usuarios;
using NeoSTP.Application.Usuarios.Dtos;

namespace NeoSTP.Api.Controllers;

[Authorize]
[Route("api/usuarios")]
public class UsuariosController : ApiControllerBase
{
    private readonly IUsuariosService _service;
    private readonly ICurrentUser _currentUser;

    public UsuariosController(IUsuariosService service, ICurrentUser currentUser)
    {
        _service = service;
        _currentUser = currentUser;
    }

    [HttpGet]
    [RequirePermiso("Core.Usuarios.Ver")]
    public async Task<IActionResult> List([FromQuery] PagedQuery query, CancellationToken ct)
        => Respond(await _service.GetListAsync(_currentUser.EmpresaId, query, ct));

    [HttpGet("{id:int}")]
    [RequirePermiso("Core.Usuarios.Ver")]
    public async Task<IActionResult> Get(int id, CancellationToken ct)
        => Respond(await _service.GetByIdAsync(_currentUser.EmpresaId, id, ct));

    [HttpPost]
    [RequirePermiso("Core.Usuarios.Crear")]
    public async Task<IActionResult> Create([FromBody] CreateUsuarioRequest request, CancellationToken ct)
        => Respond(await _service.CreateAsync(_currentUser.EmpresaId, request, _currentUser.Username, ct));

    [HttpPut("{id:int}")]
    [RequirePermiso("Core.Usuarios.Editar")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateUsuarioRequest request, CancellationToken ct)
        => Respond(await _service.UpdateAsync(_currentUser.EmpresaId, id, request, _currentUser.Username, ct));

    [HttpPatch("{id:int}/bloquear")]
    [RequirePermiso("Core.Usuarios.Bloquear")]
    public async Task<IActionResult> Bloquear(int id, CancellationToken ct)
        => Respond(await _service.BloquearAsync(_currentUser.EmpresaId, id, _currentUser.Username, ct), "Usuario bloqueado.");

    [HttpPatch("{id:int}/desbloquear")]
    [RequirePermiso("Core.Usuarios.Bloquear")]
    public async Task<IActionResult> Desbloquear(int id, CancellationToken ct)
        => Respond(await _service.DesbloquearAsync(_currentUser.EmpresaId, id, _currentUser.Username, ct), "Usuario desbloqueado.");

    [HttpPost("{id:int}/reset-password")]
    [RequirePermiso("Core.Usuarios.Editar")]
    public async Task<IActionResult> ResetPassword(int id, [FromBody] ResetPasswordRequest request, CancellationToken ct)
        => Respond(await _service.ResetPasswordAsync(_currentUser.EmpresaId, id, request, _currentUser.Username, ct), "Contraseña reseteada.");
}
