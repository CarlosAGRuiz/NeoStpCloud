using NeoSTP.Application.Dashboard.Dtos;

namespace NeoSTP.Application.Dashboard;

/// <summary>
/// Servicio de métricas del dashboard.
/// </summary>
public interface IDashboardService
{
    /// <summary>Devuelve los KPIs del mes en curso para una empresa específica.</summary>
    Task<DashboardEmpresaDto> GetDashboardEmpresaAsync(int empresaId, CancellationToken ct = default);

    /// <summary>Devuelve las métricas globales para el panel de SuperAdmin.</summary>
    Task<DashboardSuperAdminDto> GetDashboardSuperAdminAsync(CancellationToken ct = default);
}
