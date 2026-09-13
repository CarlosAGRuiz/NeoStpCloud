using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NeoSTP.Api.Authorization;
using NeoSTP.Api.Auth;
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
[RequireModule("CORE")]
[Route("api/dashboard")]
public class DashboardController : ApiControllerBase
{
    private readonly IDashboardService _dashboard;
    private readonly IGrupoDashboardService _grupo;
    private readonly ICurrentUser _currentUser;

    public DashboardController(IDashboardService dashboard, IGrupoDashboardService grupo, ICurrentUser currentUser)
    {
        _dashboard = dashboard;
        _grupo = grupo;
        _currentUser = currentUser;
    }

    /// <summary>KPIs de una empresa para el mes indicado; por defecto usa el mes en curso.</summary>
    [HttpGet("empresa")]
    public async Task<IActionResult> GetEmpresa(
        [FromQuery] int? empresaId, [FromQuery] int? anio, [FromQuery] int? mes, CancellationToken ct)
    {
        var tenant = ApiTenantResolver.Resolve(_currentUser, empresaId);
        if (!tenant.Success)
            return StatusCode(tenant.StatusCode, ApiResponse.Fail(
                tenant.Message, new[] { tenant.ErrorCode }, HttpContext.TraceIdentifier));

        var dto = await _dashboard.GetDashboardEmpresaAsync(tenant.EmpresaId!.Value, anio, mes, ct);
        return Ok(ApiResponse<Application.Dashboard.Dtos.DashboardEmpresaDto>.Ok(dto, HttpContext.TraceIdentifier));
    }

    /// <summary>
    /// Consolidado de todas las empresas donde el usuario puede operar (E5):
    /// principal + membresías activas. Sin período, el mes en curso.
    /// </summary>
    [HttpGet("grupo")]
    public async Task<IActionResult> GetGrupo([FromQuery] int? anio, [FromQuery] int? mes, CancellationToken ct)
    {
        if (_currentUser.UserId is not int userId)
            return Unauthorized(ApiResponse.Fail("Sesión inválida.", null, HttpContext.TraceIdentifier));

        return Respond(await _grupo.GetAsync(userId, anio, mes, ct));
    }

    /// <summary>Métricas globales (solo SuperAdmin).</summary>
    [HttpGet("superadmin")]
    public async Task<IActionResult> GetSuperAdmin(
        [FromQuery] int? anio, [FromQuery] int? mes, CancellationToken ct)
    {
        if (_currentUser.TipoUsuarioCodigo != "SUPERADMIN")
            return StatusCode(StatusCodes.Status403Forbidden,
                ApiResponse.Fail("Solo el SuperAdmin puede acceder a este endpoint.", new[] { "FORBIDDEN" }, HttpContext.TraceIdentifier));

        var dto = await _dashboard.GetDashboardSuperAdminAsync(anio, mes, ct);
        return Ok(ApiResponse<Application.Dashboard.Dtos.DashboardSuperAdminDto>.Ok(dto, HttpContext.TraceIdentifier));
    }
}
