using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NeoSTP.Application.Licenciamiento;

namespace NeoSTP.Api.Controllers;

[Authorize]
[Route("api/planes")]
public class PlanesController : ApiControllerBase
{
    private readonly IPlanesService _service;

    public PlanesController(IPlanesService service) { _service = service; }

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) => Respond(await _service.GetListAsync(ct));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id, CancellationToken ct) => Respond(await _service.GetByIdAsync(id, ct));
}

[Authorize]
[Route("api/modulos")]
public class ModulosController : ApiControllerBase
{
    private readonly IModulosService _service;

    public ModulosController(IModulosService service) { _service = service; }

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) => Respond(await _service.GetListAsync(ct));
}
