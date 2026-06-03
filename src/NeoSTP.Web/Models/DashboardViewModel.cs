using NeoSTP.Application.Dashboard.Dtos;
using NeoSTP.Application.Onboarding.Dtos;

namespace NeoSTP.Web.Models;

/// <summary>Modelo unificado para las vistas de dashboard.</summary>
public class DashboardViewModel
{
    public string Username { get; set; } = string.Empty;
    public bool EsSuperAdmin { get; set; }

    /// <summary>Disponible cuando el usuario tiene contexto de empresa (usuario de empresa o SuperAdmin en modo soporte).</summary>
    public DashboardEmpresaDto? EmpresaDashboard { get; set; }
    public string? EmpresaNombre { get; set; }

    /// <summary>Estado de activación (onboarding) de la empresa. Null para SuperAdmin sin empresa.</summary>
    public OnboardingEstadoDto? Onboarding { get; set; }

    /// <summary>Disponible solo para SuperAdmin sin modo soporte activo.</summary>
    public DashboardSuperAdminDto? SuperAdminDashboard { get; set; }
}
