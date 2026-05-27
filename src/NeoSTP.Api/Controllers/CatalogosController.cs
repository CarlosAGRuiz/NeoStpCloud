using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Catalogos;

namespace NeoSTP.Api.Controllers;

[Authorize]
[Route("api/catalogos")]
public class CatalogosController : ApiControllerBase
{
    private readonly ICatalogosService _service;
    private readonly ICurrentUser _currentUser;

    public CatalogosController(ICatalogosService service, ICurrentUser currentUser)
    {
        _service = service;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
        => Respond(await _service.GetListAsync(_currentUser.EmpresaId, ct));

    [HttpGet("{codigo}/items")]
    public async Task<IActionResult> Items(string codigo, CancellationToken ct)
        => Respond(await _service.GetItemsAsync(codigo, _currentUser.EmpresaId, ct));
}
