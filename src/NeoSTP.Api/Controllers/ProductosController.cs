using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NeoSTP.Api.Authorization;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Common;
using NeoSTP.Application.Productos;
using NeoSTP.Application.Productos.Dtos;

namespace NeoSTP.Api.Controllers;

[Authorize]
[Route("api/productos")]
public class ProductosController : ApiControllerBase
{
    private readonly IProductosService _service;
    private readonly ICurrentUser _currentUser;

    public ProductosController(IProductosService service, ICurrentUser currentUser)
    {
        _service = service;
        _currentUser = currentUser;
    }

    [HttpGet]
    [RequirePermiso("Productos.Ver")]
    public async Task<IActionResult> List([FromQuery] PagedQuery query, [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        return Respond(await _service.GetListAsync(eid, query, ct));
    }

    [HttpGet("{id:int}")]
    [RequirePermiso("Productos.Ver")]
    public async Task<IActionResult> Get(int id, [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        return Respond(await _service.GetByIdAsync(eid, id, ct));
    }

    [HttpPost]
    [RequirePermiso("Productos.Crear")]
    public async Task<IActionResult> Create([FromBody] CreateProductoRequest request, [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        return Respond(await _service.CreateAsync(eid, request, _currentUser.Username, ct));
    }

    [HttpPut("{id:int}")]
    [RequirePermiso("Productos.Editar")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateProductoRequest request, [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        return Respond(await _service.UpdateAsync(eid, id, request, _currentUser.Username, ct));
    }

    [HttpPatch("{id:int}/inactivar")]
    [RequirePermiso("Productos.Editar")]
    public async Task<IActionResult> Inactivar(int id, [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        return Respond(await _service.InactivarAsync(eid, id, _currentUser.Username, ct), "Producto inactivado.");
    }

    private int? Resolve(int? fromRequest) => _currentUser.EmpresaId ?? fromRequest;

    private object NoTenant() => Shared.ApiResponse.Fail(
        "No se pudo determinar la empresa. Si eres SuperAdmin, envía empresaId.",
        new[] { "AUTH_NO_TENANT" }, HttpContext.TraceIdentifier);
}
