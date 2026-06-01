using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Dte.Diagnostico;
using NeoSTP.Application.Dte.Diagnostico.Dtos;

namespace NeoSTP.Api.Controllers;

[Authorize]
[Route("api/dte/diagnostico")]
public class DteDiagnosticoController : ApiControllerBase
{
    private readonly IDiagnosticoHaciendaService _service;
    private readonly ICurrentUser _currentUser;

    public DteDiagnosticoController(IDiagnosticoHaciendaService service, ICurrentUser currentUser)
    {
        _service = service;
        _currentUser = currentUser;
    }

    /// <summary>Resumen de errores de la empresa (totales, no resueltos, hoy, top códigos).</summary>
    [HttpGet("resumen")]
    public async Task<IActionResult> Resumen(CancellationToken ct)
    {
        if (ResolveEmpresaId() is not int eid) return BadRequest("EmpresaId requerido.");
        var resumen = await _service.ObtenerResumenAsync(eid, ct);
        return Ok(resumen);
    }

    /// <summary>Lista paginada de ocurrencias con filtros.</summary>
    [HttpGet("errores")]
    public async Task<IActionResult> ListarErrores(
        [FromQuery] string? codigoError,
        [FromQuery] string? fuente,
        [FromQuery] DateTime? desde,
        [FromQuery] DateTime? hasta,
        [FromQuery] bool? soloNoResueltas,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        if (ResolveEmpresaId() is not int eid) return BadRequest("EmpresaId requerido.");
        var filtro = new DiagnosticoFiltroRequest(codigoError, fuente, desde, hasta, soloNoResueltas, page, pageSize);
        var result = await _service.ListarOcurrenciasAsync(eid, filtro, ct);
        return Ok(result);
    }

    /// <summary>Catálogo completo de errores conocidos.</summary>
    [HttpGet("catalogo")]
    public async Task<IActionResult> Catalogo(CancellationToken ct)
    {
        var catalogo = await _service.ListarCatalogoAsync(ct);
        return Ok(catalogo);
    }

    /// <summary>Diagnóstico de un documento DTE específico.</summary>
    [HttpGet("documentos/{id:int}")]
    public async Task<IActionResult> DiagnosticoDocumento(int id, CancellationToken ct)
    {
        if (ResolveEmpresaId() is not int eid) return BadRequest("EmpresaId requerido.");
        var dto = await _service.ObtenerDiagnosticoDocumentoAsync(eid, id, ct);
        return dto is null ? NotFound() : Ok(dto);
    }

    /// <summary>Diagnóstico de un evento DTE específico.</summary>
    [HttpGet("eventos/{id:int}")]
    public async Task<IActionResult> DiagnosticoEvento(int id, CancellationToken ct)
    {
        if (ResolveEmpresaId() is not int eid) return BadRequest("EmpresaId requerido.");
        var dto = await _service.ObtenerDiagnosticoEventoAsync(eid, id, ct);
        return dto is null ? NotFound() : Ok(dto);
    }

    /// <summary>Marca una ocurrencia como resuelta.</summary>
    [HttpPost("errores/{id:int}/resolver")]
    public async Task<IActionResult> MarcarResuelta(int id, CancellationToken ct)
    {
        if (ResolveEmpresaId() is not int eid) return BadRequest("EmpresaId requerido.");
        var result = await _service.MarcarResueltaAsync(eid, id, _currentUser.Username ?? "api", ct);
        return Respond(result);
    }

    /// <summary>Sincroniza ocurrencias desde los datos históricos existentes (certificación, eventos).</summary>
    [HttpPost("sincronizar")]
    public async Task<IActionResult> Sincronizar(CancellationToken ct)
    {
        if (ResolveEmpresaId() is not int eid) return BadRequest("EmpresaId requerido.");
        var importadas = await _service.SincronizarOcurrenciasAsync(eid, ct);
        return Ok(new { importadas, mensaje = $"Se importaron {importadas} ocurrencias nuevas." });
    }

    private int? ResolveEmpresaId()
        => _currentUser.TipoUsuarioCodigo == "SUPERADMIN"
            ? (int?)null  // SuperAdmin debe pasar empresaId desde context de soporte
            : _currentUser.EmpresaId;
}
