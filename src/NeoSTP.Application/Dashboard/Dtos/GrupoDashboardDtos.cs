namespace NeoSTP.Application.Dashboard.Dtos;

// ─────────────────────────────────────────────
//  Dashboard consolidado de grupo (E5)
// ─────────────────────────────────────────────

/// <summary>
/// Vista consolidada de todas las empresas donde el usuario puede operar (E5):
/// su empresa principal más las membresías activas (E1). Pensado para contadores
/// que llevan varios clientes y para holdings con varias sociedades.
/// </summary>
public sealed class GrupoDashboardDto
{
    public int Anio { get; set; }
    public int Mes { get; set; }

    /// <summary>Una fila por empresa, ordenada por ventas del período (desc).</summary>
    public List<EmpresaGrupoResumenDto> Empresas { get; set; } = [];

    // ── Totales del grupo ─────────────────────────
    public int EmpresasTotal => Empresas.Count;
    public int EmpresasSuspendidas => Empresas.Count(e => !e.Activa);
    public int DteMes => Empresas.Sum(e => e.DteMes);
    public decimal VentasMes => Empresas.Sum(e => e.VentasMes);
    public decimal IvaDebitoMes => Empresas.Sum(e => e.IvaDebitoMes);
    public int Rechazados => Empresas.Sum(e => e.Rechazados);
    public int Pendientes => Empresas.Sum(e => e.Pendientes);
    public decimal PorCobrar => Empresas.Sum(e => e.PorCobrar);
    public decimal Vencido => Empresas.Sum(e => e.Vencido);
    public int FacturasVencidas => Empresas.Sum(e => e.FacturasVencidas);
    public int AlertasActivas => Empresas.Sum(e => e.AlertasActivas);

    /// <summary>Empresas que requieren atención: suspendidas, con rechazos o con cartera vencida.</summary>
    public int EmpresasConPendientes =>
        Empresas.Count(e => !e.Activa || e.Rechazados > 0 || e.FacturasVencidas > 0);
}

/// <summary>Resumen de una empresa dentro del dashboard de grupo.</summary>
public sealed class EmpresaGrupoResumenDto
{
    public int EmpresaId { get; set; }
    public string Nombre { get; set; } = string.Empty;

    /// <summary>True si es la empresa principal del usuario (no una membresía).</summary>
    public bool EsPrincipal { get; set; }

    /// <summary>Rol del usuario en esa empresa (null en la principal: usa sus roles propios).</summary>
    public string? RolNombre { get; set; }

    /// <summary>False si la empresa está suspendida/inactiva (no se puede operar).</summary>
    public bool Activa { get; set; }

    // ── DTE del período ───────────────────────────
    public int DteMes { get; set; }
    public decimal VentasMes { get; set; }

    /// <summary>IVA débito fiscal del período (documentos PROCESADO).</summary>
    public decimal IvaDebitoMes { get; set; }

    public int Rechazados { get; set; }
    public int Pendientes { get; set; }

    // ── Cartera (a la fecha, no del período) ──────
    public decimal PorCobrar { get; set; }
    public decimal Vencido { get; set; }
    public int FacturasVencidas { get; set; }

    // ── Alertas ───────────────────────────────────
    public int AlertasActivas { get; set; }

    /// <summary>True si la empresa necesita atención del contador/administrador del grupo.</summary>
    public bool RequiereAtencion => !Activa || Rechazados > 0 || FacturasVencidas > 0;
}
