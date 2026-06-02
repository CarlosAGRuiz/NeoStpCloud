using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Lookups;
using NeoSTP.Shared;

namespace NeoSTP.Api.Controllers;

/// <summary>
/// Lookups para poblar selects, cascadas y autocompletes desde la UI o integraciones.
/// Cualquier usuario autenticado puede leerlos (datos de catálogo / maestros de su empresa).
/// </summary>
[Authorize]
[Route("api/lookups")]
public class LookupsController : ControllerBase
{
    private readonly ILookupService _lookups;
    private readonly ICurrentUser _currentUser;

    public LookupsController(ILookupService lookups, ICurrentUser currentUser)
    {
        _lookups = lookups;
        _currentUser = currentUser;
    }

    private int? EmpresaId => _currentUser.EmpresaId;

    private IActionResult Ok(IReadOnlyList<LookupItem> items)
        => base.Ok(ApiResponse<IReadOnlyList<LookupItem>>.Ok(items, traceId: HttpContext.TraceIdentifier));

    [HttpGet("catalogo/{codigo}")]
    public async Task<IActionResult> Catalogo(string codigo, [FromQuery] string? parent, CancellationToken ct)
        => Ok(await _lookups.GetCatalogoAsync(codigo, EmpresaId, parent, ct));

    [HttpGet("departamentos")]
    public async Task<IActionResult> Departamentos(CancellationToken ct)
        => Ok(await _lookups.GetDepartamentosAsync(EmpresaId, ct));

    [HttpGet("municipios")]
    public async Task<IActionResult> Municipios([FromQuery] string departamento, CancellationToken ct)
        => Ok(await _lookups.GetMunicipiosAsync(departamento ?? string.Empty, EmpresaId, ct));

    [HttpGet("distritos")]
    public async Task<IActionResult> Distritos([FromQuery] string municipio, CancellationToken ct)
        => Ok(await _lookups.GetDistritosAsync(municipio ?? string.Empty, EmpresaId, ct));

    [HttpGet("clientes")]
    public async Task<IActionResult> Clientes([FromQuery] string? search, CancellationToken ct)
        => EmpresaId is int e ? Ok(await _lookups.BuscarClientesAsync(e, search, ct: ct))
                              : Ok(Array.Empty<LookupItem>());

    [HttpGet("productos")]
    public async Task<IActionResult> Productos([FromQuery] string? search, CancellationToken ct)
        => EmpresaId is int e ? Ok(await _lookups.BuscarProductosAsync(e, search, ct: ct))
                              : Ok(Array.Empty<LookupItem>());

    [HttpGet("sucursales")]
    public async Task<IActionResult> Sucursales(CancellationToken ct)
        => EmpresaId is int e ? Ok(await _lookups.GetSucursalesAsync(e, ct))
                              : Ok(Array.Empty<LookupItem>());
}
