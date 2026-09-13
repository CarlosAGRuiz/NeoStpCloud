using NeoSTP.Application.Dashboard.Dtos;

namespace NeoSTP.Application.Dashboard;

/// <summary>
/// Servicio de métricas del dashboard.
/// </summary>
public interface IDashboardService
{
    /// <summary>Devuelve los KPIs de un mes para una empresa específica. Por defecto usa el mes actual.</summary>
    Task<DashboardEmpresaDto> GetDashboardEmpresaAsync(int empresaId, int? anio = null, int? mes = null, CancellationToken ct = default);

    /// <summary>Devuelve las métricas globales para el panel de SuperAdmin.</summary>
    Task<DashboardSuperAdminDto> GetDashboardSuperAdminAsync(int? anio = null, int? mes = null, CancellationToken ct = default);
}
