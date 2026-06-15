using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NeoSTP.Api.Authorization;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Reportes;
using NeoSTP.Shared;

namespace NeoSTP.Api.Controllers;

/// <summary>
/// NEOBI fiscal — libros IVA mensuales (ventas consumidor/contribuyentes, compras) y
/// resumen F-07. Requiere el módulo NEOBI activo. Permiso: Reportes.Ver.
/// </summary>
[Authorize]
[RequireModule("NEOBI")]
[Route("api/reportes/fiscal")]
public class ReportesFiscalController : ApiControllerBase
{
    private readonly IReporteFiscalService _reportes;
    private readonly ICurrentUser _currentUser;

    public ReportesFiscalController(IReporteFiscalService reportes, ICurrentUser currentUser)
    {
        _reportes = reportes;
        _currentUser = currentUser;
    }

    [HttpGet("libro-ventas-consumidor")]
    [RequirePermiso("Reportes.Ver")]
    public async Task<IActionResult> LibroVentasConsumidor([FromQuery] int anio, [FromQuery] int mes, [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        return Respond(await _reportes.LibroVentasConsumidorAsync(eid, anio, mes, ct));
    }

    [HttpGet("libro-ventas-contribuyentes")]
    [RequirePermiso("Reportes.Ver")]
    public async Task<IActionResult> LibroVentasContribuyentes([FromQuery] int anio, [FromQuery] int mes, [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        return Respond(await _reportes.LibroVentasContribuyentesAsync(eid, anio, mes, ct));
    }

    [HttpGet("libro-compras")]
    [RequirePermiso("Reportes.Ver")]
    public async Task<IActionResult> LibroCompras([FromQuery] int anio, [FromQuery] int mes, [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        return Respond(await _reportes.LibroComprasAsync(eid, anio, mes, ct));
    }

    [HttpGet("f07")]
    [RequirePermiso("Reportes.Ver")]
    public async Task<IActionResult> ResumenF07([FromQuery] int anio, [FromQuery] int mes, [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        return Respond(await _reportes.ResumenF07Async(eid, anio, mes, ct));
    }

    [HttpGet("libro-ventas-consumidor/csv")]
    [RequirePermiso("Reportes.Ver")]
    [Produces("text/csv")]
    public async Task<IActionResult> LibroVentasConsumidorCsv([FromQuery] int anio, [FromQuery] int mes, [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        var r = await _reportes.LibroVentasConsumidorCsvAsync(eid, anio, mes, ct);
        if (r.IsFailure) return Respond(r);
        return File(r.Value!, "text/csv", $"libro_ventas_consumidor_{anio:0000}_{mes:00}.csv");
    }

    [HttpGet("libro-ventas-contribuyentes/csv")]
    [RequirePermiso("Reportes.Ver")]
    [Produces("text/csv")]
    public async Task<IActionResult> LibroVentasContribuyentesCsv([FromQuery] int anio, [FromQuery] int mes, [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        var r = await _reportes.LibroVentasContribuyentesCsvAsync(eid, anio, mes, ct);
        if (r.IsFailure) return Respond(r);
        return File(r.Value!, "text/csv", $"libro_ventas_contribuyentes_{anio:0000}_{mes:00}.csv");
    }

    [HttpGet("libro-compras/csv")]
    [RequirePermiso("Reportes.Ver")]
    [Produces("text/csv")]
    public async Task<IActionResult> LibroComprasCsv([FromQuery] int anio, [FromQuery] int mes, [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (Resolve(empresaId) is not int eid) return BadRequest(NoTenant());
        var r = await _reportes.LibroComprasCsvAsync(eid, anio, mes, ct);
        if (r.IsFailure) return Respond(r);
        return File(r.Value!, "text/csv", $"libro_compras_{anio:0000}_{mes:00}.csv");
    }

    private int? Resolve(int? fromRequest) => _currentUser.EmpresaId ?? fromRequest;

    private object NoTenant() => ApiResponse.Fail(
        "No se pudo determinar la empresa. Si eres SuperAdmin, envía empresaId.",
        new[] { "AUTH_NO_TENANT" }, HttpContext.TraceIdentifier);
}
