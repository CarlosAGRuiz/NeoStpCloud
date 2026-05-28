using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Dashboard;
using NeoSTP.Shared;

namespace NeoSTP.Api.Controllers;

/// <summary>
/// Métricas del dashboard.
///   GET /api/dashboard/empresa   → KPIs del mes para la empresa del token (o ?empresaId= para SuperAdmin)
///   GET /api/dashboard/superadmin → métricas globales (solo SUPERADMIN)
/// </summary>
[Authorize]
[Route("api/dashboard")]
public class DashboardController : ApiControllerBase
{
    private readonly IDashboardService _dashboard;
    private readonly ICurrentUser _currentUser;

    public DashboardController(IDashboardService dashboard, ICurrentUser currentUser)
    {
        _dashboard = dashboard;
        _currentUser = currentUser;
    }

    /// <summary>KPIs del mes en curso para una empresa.</summary>
    [HttpGet("empresa")]
    public async Task<IActionResult> GetEmpresa([FromQuery] int? empresaId, CancellationToken ct)
    {
        var eid = _currentUser.EmpresaId ?? empresaId;
        if (eid is null)
            return BadRequest(ApiResponse.Fail(
                "No se pudo determinar la empresa. Si eres SuperAdmin, envía ?empresaId=.",
                new[] { "AUTH_NO_TENANT" }, HttpContext.TraceIdentifier));

        var dto = await _dashboard.GetDashboardEmpresaAsync(eid.Value, ct);
        return Ok(ApiResponse<Application.Dashboard.Dtos.DashboardEmpresaDto>.Ok(dto, HttpContext.TraceIdentifier));
    }

    /// <summary>Métricas globales (solo SuperAdmin).</summary>
    [HttpGet("superadmin")]
    public async Task<IActionResult> GetSuperAdmin(CancellationToken ct)
    {
        if (_currentUser.TipoUsuarioCodigo != "SUPERADMIN")
            return StatusCode(StatusCodes.Status403Forbidden,
                ApiResponse.Fail("Solo el SuperAdmin puede acceder a este endpoint.", new[] { "FORBIDDEN" }, HttpContext.TraceIdentifier));

        var dto = await _dashboard.GetDashboardSuperAdminAsync(ct);
        return Ok(ApiResponse<Application.Dashboard.Dtos.DashboardSuperAdminDto>.Ok(dto, HttpContext.TraceIdentifier));
    }
}
