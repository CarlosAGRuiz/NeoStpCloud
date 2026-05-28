namespace NeoSTP.Application.Dashboard.Dtos;

// ─────────────────────────────────────────────
//  Dashboard de empresa (dashboard principal)
// ─────────────────────────────────────────────

/// <summary>KPIs y tendencias del dashboard por empresa para el mes en curso.</summary>
public class DashboardEmpresaDto
{
    // ── KPIs del mes ──────────────────────────────
    public int DteHoy { get; set; }
    public int DteMes { get; set; }
    public decimal TotalPagarMes { get; set; }

    // ── Por estado (acumulado total) ───────────────
    public int Procesados { get; set; }
    public int Rechazados { get; set; }
    public int Contingencias { get; set; }

    /// <summary>BORRADOR + GENERADO + VALIDADO + FIRMADO + ENVIADO</summary>
    public int Pendientes { get; set; }

    // ── Plan contratado ───────────────────────────
    public string? PlanNombre { get; set; }
    public int? LimiteDteMensual { get; set; }

    /// <summary>Porcentaje de consumo del cupo DTE mensual (0-100). 0 si no hay límite.</summary>
    public int PorcentajeUsoDte =>
        LimiteDteMensual is > 0
            ? Math.Min(100, (int)Math.Round(DteMes * 100.0 / LimiteDteMensual.Value))
            : 0;

    // ── Desgloses del mes ────────────────────────
    public List<DteEstadoResumenDto> PorEstado { get; set; } = [];
    public List<DteTipoResumenDto> PorTipo { get; set; } = [];

    /// <summary>Emisiones diarias de los últimos 30 días (incl. días sin documentos = 0).</summary>
    public List<DteDiarioDto> TendenciaDiaria { get; set; } = [];
}

public class DteEstadoResumenDto
{
    public string Estado { get; set; } = string.Empty;
    public int Cantidad { get; set; }
    public decimal TotalPagar { get; set; }
}

public class DteTipoResumenDto
{
    public string TipoCodigo { get; set; } = string.Empty;
    public string TipoNombre { get; set; } = string.Empty;
    public int Cantidad { get; set; }
    public decimal TotalPagar { get; set; }
}

public class DteDiarioDto
{
    public DateOnly Fecha { get; set; }
    public int Cantidad { get; set; }
    public decimal TotalPagar { get; set; }
}

// ─────────────────────────────────────────────
//  Dashboard SuperAdmin
// ─────────────────────────────────────────────

/// <summary>Métricas globales para el panel de SuperAdmin.</summary>
public class DashboardSuperAdminDto
{
    // ── KPIs globales ──────────────────────────────
    public int EmpresasActivas { get; set; }
    public int EmpresasTotal { get; set; }
    public int UsuariosActivos { get; set; }
    public int DteTotalMes { get; set; }
    public decimal FacturacionTotalMes { get; set; }

    // ── Detalle ────────────────────────────────────
    public List<PlanResumenDto> ResumenPorPlan { get; set; } = [];
    public List<AlertaPlanDto> AlertasPlanProximoVencer { get; set; } = [];
    public List<EmpresaMetricasDto> TopEmpresasDteMes { get; set; } = [];
}

public class PlanResumenDto
{
    public string PlanNombre { get; set; } = string.Empty;
    public int EmpresasCount { get; set; }
    public decimal IngresosMensuales { get; set; }
}

public class AlertaPlanDto
{
    public int EmpresaId { get; set; }
    public string RazonSocial { get; set; } = string.Empty;
    public string PlanNombre { get; set; } = string.Empty;
    public DateTime FechaFin { get; set; }
    public int DiasRestantes { get; set; }
}

public class EmpresaMetricasDto
{
    public int EmpresaId { get; set; }
    public string RazonSocial { get; set; } = string.Empty;
    public int DteCount { get; set; }
    public decimal TotalPagar { get; set; }
}
