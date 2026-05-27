using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NeoSTP.Api.Authorization;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Common;
using NeoSTP.Application.Empresas;
using NeoSTP.Application.Empresas.Dtos;

namespace NeoSTP.Api.Controllers;

[Authorize]
[Route("api/empresas")]
public class EmpresasController : ApiControllerBase
{
    private readonly IEmpresasService _service;
    private readonly ICurrentUser _currentUser;

    public EmpresasController(IEmpresasService service, ICurrentUser currentUser)
    {
        _service = service;
        _currentUser = currentUser;
    }

    /// <summary>SuperAdmin ve todas; usuario de empresa solo la suya.</summary>
    [HttpGet]
    [RequirePermiso("Core.Empresa.Ver")]
    public async Task<IActionResult> List([FromQuery] PagedQuery query, CancellationToken ct)
        => Respond(await _service.GetListAsync(_currentUser.EmpresaId, query, ct));

    [HttpGet("{id:int}")]
    [RequirePermiso("Core.Empresa.Ver")]
    public async Task<IActionResult> Get(int id, CancellationToken ct)
        => Respond(await _service.GetByIdAsync(_currentUser.EmpresaId, id, ct));

    /// <summary>Solo SuperAdmin puede crear empresas (no se requiere EmpresaId previo).</summary>
    [HttpPost]
    [RequirePermiso("SuperAdmin.Empresas.Administrar")]
    public async Task<IActionResult> Create([FromBody] CreateEmpresaRequest request, CancellationToken ct)
        => Respond(await _service.CreateAsync(request, _currentUser.Username, ct));

    [HttpPut("{id:int}")]
    [RequirePermiso("Core.Empresa.Editar")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateEmpresaRequest request, CancellationToken ct)
        => Respond(await _service.UpdateAsync(_currentUser.EmpresaId, id, request, _currentUser.Username, ct));

    [HttpGet("{id:int}/licencia")]
    [RequirePermiso("Core.Empresa.Ver")]
    public async Task<IActionResult> Licencia(int id, CancellationToken ct)
    {
        if (_currentUser.EmpresaId is not null && _currentUser.EmpresaId != id)
        {
            return Forbid();
        }
        return Respond(await _service.GetLicenciaAsync(id, ct));
    }

    [HttpPost("{id:int}/plan")]
    [RequirePermiso("SuperAdmin.Planes.Administrar")]
    public async Task<IActionResult> AsignarPlan(int id, [FromBody] AsignarPlanRequest request, CancellationToken ct)
        => Respond(await _service.AsignarPlanAsync(id, request, _currentUser.Username, ct));

    [HttpPost("{id:int}/modulos/{moduloId:int}/activar")]
    [RequirePermiso("SuperAdmin.Planes.Administrar")]
    public async Task<IActionResult> ActivarModulo(int id, int moduloId, CancellationToken ct)
        => Respond(await _service.ActivarModuloAsync(id, moduloId, _currentUser.Username, ct));

    [HttpPost("{id:int}/modulos/{moduloId:int}/desactivar")]
    [RequirePermiso("SuperAdmin.Planes.Administrar")]
    public async Task<IActionResult> DesactivarModulo(int id, int moduloId, CancellationToken ct)
        => Respond(await _service.DesactivarModuloAsync(id, moduloId, _currentUser.Username, ct));
}
