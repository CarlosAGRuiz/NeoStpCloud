using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NeoSTP.Api.Authorization;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Dte.Contingencia;

namespace NeoSTP.Api.Controllers;

[Authorize]
[Route("api/dte/contingencia")]
public class DteContingenciaController : ApiControllerBase
{
    private readonly IContingenciaLoteService _service;
    private readonly ICurrentUser _currentUser;

    public DteContingenciaController(IContingenciaLoteService service, ICurrentUser currentUser)
    {
        _service = service;
        _currentUser = currentUser;
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Queries
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>Resumen del estado de contingencia: conteos y alertas de vencimiento.</summary>
    [HttpGet("resumen")]
    [RequirePermiso("DTE.Contingencia")]
    public async Task<IActionResult> Resumen([FromQuery] int? empresaId, CancellationToken ct)
    {
        if (ResolveEmpresaId(empresaId) is not int eid) return BadRequest(NoTenant());
        return Ok(NeoSTP.Shared.ApiResponse<object>.Ok(
            await _service.ObtenerResumenAsync(eid, ct),
            traceId: HttpContext.TraceIdentifier));
    }

    /// <summary>Lista de DTE en estado CONTINGENCIA pendientes de resolución.</summary>
    [HttpGet("documentos")]
    [RequirePermiso("DTE.Contingencia")]
    public async Task<IActionResult> Documentos([FromQuery] int? empresaId, CancellationToken ct)
    {
        if (ResolveEmpresaId(empresaId) is not int eid) return BadRequest(NoTenant());
        return Ok(NeoSTP.Shared.ApiResponse<object>.Ok(
            await _service.ListarDocumentosPendientesAsync(eid, ct),
            traceId: HttpContext.TraceIdentifier));
    }

    /// <summary>Lista de lotes de contingencia con su estado.</summary>
    [HttpGet("lotes")]
    [RequirePermiso("DTE.Contingencia")]
    public async Task<IActionResult> Lotes([FromQuery] int? empresaId, CancellationToken ct)
    {
        if (ResolveEmpresaId(empresaId) is not int eid) return BadRequest(NoTenant());
        return Ok(NeoSTP.Shared.ApiResponse<object>.Ok(
            await _service.ListarLotesAsync(eid, ct),
            traceId: HttpContext.TraceIdentifier));
    }

    /// <summary>Detalle de un lote específico con sus detalles por DTE.</summary>
    [HttpGet("lotes/{loteId:int}")]
    [RequirePermiso("DTE.Contingencia")]
    public async Task<IActionResult> ObtenerLote(int loteId, [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (ResolveEmpresaId(empresaId) is not int eid) return BadRequest(NoTenant());
        var lote = await _service.ObtenerLoteAsync(loteId, eid, ct);
        if (lote is null) return NotFound();
        return Ok(NeoSTP.Shared.ApiResponse<object>.Ok(lote, traceId: HttpContext.TraceIdentifier));
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Comandos
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>Crea y envía el lote de contingencia para un evento dado.</summary>
    [HttpPost("lotes/crear")]
    [RequirePermiso("DTE.Contingencia")]
    public async Task<IActionResult> CrearLote([FromBody] CrearLoteRequest body, [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (ResolveEmpresaId(empresaId) is not int eid) return BadRequest(NoTenant());
        return Respond(await _service.CrearYEnviarLoteAsync(
            body.EventoContingenciaId, eid, _currentUser.Username ?? "api", ct));
    }

    /// <summary>Consulta el estado actualizado de un lote en Hacienda.</summary>
    [HttpPost("lotes/{loteId:int}/consultar")]
    [RequirePermiso("DTE.Contingencia")]
    public async Task<IActionResult> ConsultarLote(int loteId, [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (ResolveEmpresaId(empresaId) is not int eid) return BadRequest(NoTenant());
        return Respond(await _service.ConsultarLoteAsync(loteId, eid, ct));
    }

    /// <summary>Marca un DTE en CONTINGENCIA para reintento manual en el próximo ciclo del Worker.</summary>
    [HttpPost("documentos/{dteId:int}/reintentar")]
    [RequirePermiso("DTE.Contingencia")]
    public async Task<IActionResult> Reintentar(int dteId, [FromQuery] int? empresaId, CancellationToken ct)
    {
        if (ResolveEmpresaId(empresaId) is not int eid) return BadRequest(NoTenant());
        return Respond(await _service.ReintentarDocumentoAsync(dteId, eid, ct));
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────────────────────

    private int? ResolveEmpresaId(int? fromRequest) => _currentUser.EmpresaId ?? fromRequest;

    private object NoTenant() => NeoSTP.Shared.ApiResponse.Fail(
        "No se pudo determinar la empresa. SuperAdmin debe pasar empresaId.",
        new[] { "AUTH_NO_TENANT" }, HttpContext.TraceIdentifier);
}

/// <summary>Body para crear un lote desde un evento de contingencia.</summary>
public record CrearLoteRequest(int EventoContingenciaId);
