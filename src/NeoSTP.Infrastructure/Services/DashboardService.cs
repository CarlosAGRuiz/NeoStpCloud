using Microsoft.EntityFrameworkCore;
using NeoSTP.Application.Dashboard;
using NeoSTP.Application.Dashboard.Dtos;
using NeoSTP.Domain.Common;
using NeoSTP.Domain.Core.Dte;
using NeoSTP.Infrastructure.Persistence;

namespace NeoSTP.Infrastructure.Services;

/// <summary>
/// Agrega métricas del dashboard desde la BD directamente mediante EF Core.
/// Todas las fechas se comparan en UTC.
/// </summary>
public class DashboardService : IDashboardService
{
    private readonly NeoStpDbContext _db;

    public DashboardService(NeoStpDbContext db) => _db = db;

    // ─────────────────────────────────────────────────────────────
    //  Dashboard empresa
    // ─────────────────────────────────────────────────────────────

    public async Task<DashboardEmpresaDto> GetDashboardEmpresaAsync(int empresaId, CancellationToken ct = default)
    {
        var hoy = DateTime.UtcNow.Date;
        var inicioMes = new DateTime(hoy.Year, hoy.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var inicio30 = hoy.AddDays(-29);

        var base_ = _db.DteDocumentos.AsNoTracking().Where(d => d.EmpresaId == empresaId);

        // ── KPIs simples ──────────────────────────────────────────
        var dteHoy = await base_.CountAsync(d => d.FechaEmision == hoy, ct);
        var dteMes = await base_.CountAsync(d => d.FechaEmision >= inicioMes, ct);

        var totalPagarMes = await base_
            .Where(d => d.FechaEmision >= inicioMes && d.EstadoCodigo == DteEstadoCodigos.Procesado)
            .SumAsync(d => (decimal?)d.TotalPagar, ct) ?? 0m;

        var procesados = await base_.CountAsync(d => d.EstadoCodigo == DteEstadoCodigos.Procesado, ct);
        var rechazados = await base_.CountAsync(d => d.EstadoCodigo == DteEstadoCodigos.Rechazado, ct);
        var contingencias = await base_.CountAsync(d => d.EstadoCodigo == DteEstadoCodigos.Contingencia, ct);

        var estadosPendientes = new[]
        {
            DteEstadoCodigos.Borrador, DteEstadoCodigos.Generado,
            DteEstadoCodigos.Validado, DteEstadoCodigos.Firmado, DteEstadoCodigos.Enviado,
        };
        var pendientes = await base_.CountAsync(d => estadosPendientes.Contains(d.EstadoCodigo), ct);

        // ── Por estado (mes actual) ───────────────────────────────
        var porEstado = await base_
            .Where(d => d.FechaEmision >= inicioMes)
            .GroupBy(d => d.EstadoCodigo)
            .Select(g => new DteEstadoResumenDto
            {
                Estado = g.Key,
                Cantidad = g.Count(),
                TotalPagar = g.Sum(d => d.TotalPagar),
            })
            .OrderByDescending(x => x.Cantidad)
            .ToListAsync(ct);

        // ── Por tipo (mes actual) ─────────────────────────────────
        var porTipoRaw = await base_
            .Where(d => d.FechaEmision >= inicioMes)
            .GroupBy(d => d.TipoDteCodigo)
            .Select(g => new { TipoCodigo = g.Key, Cantidad = g.Count(), TotalPagar = g.Sum(d => d.TotalPagar) })
            .OrderByDescending(x => x.Cantidad)
            .ToListAsync(ct);

        var porTipo = porTipoRaw.Select(x => new DteTipoResumenDto
        {
            TipoCodigo = x.TipoCodigo,
            TipoNombre = ResolveTipoNombre(x.TipoCodigo),
            Cantidad = x.Cantidad,
            TotalPagar = x.TotalPagar,
        }).ToList();

        // ── Tendencia diaria (últimos 30 días) ────────────────────
        var tendenciaRaw = await base_
            .Where(d => d.FechaEmision >= inicio30)
            .GroupBy(d => d.FechaEmision)
            .Select(g => new { Fecha = g.Key, Cantidad = g.Count(), TotalPagar = g.Sum(d => d.TotalPagar) })
            .OrderBy(x => x.Fecha)
            .ToListAsync(ct);

        // Rellenar días sin documentos con 0
        var tendencia = new List<DteDiarioDto>(30);
        for (var d = inicio30; d <= hoy; d = d.AddDays(1))
        {
            var raw = tendenciaRaw.FirstOrDefault(x => x.Fecha.Date == d);
            tendencia.Add(new DteDiarioDto
            {
                Fecha = DateOnly.FromDateTime(d),
                Cantidad = raw?.Cantidad ?? 0,
                TotalPagar = raw?.TotalPagar ?? 0m,
            });
        }

        // ── Plan activo de la empresa ─────────────────────────────
        var plan = await _db.EmpresaPlanes
            .AsNoTracking()
            .Include(ep => ep.Plan)
            .Where(ep => ep.EmpresaId == empresaId && ep.EstadoCodigo == EstadoCodes.Activo)
            .OrderByDescending(ep => ep.FechaInicio)
            .FirstOrDefaultAsync(ct);

        return new DashboardEmpresaDto
        {
            DteHoy = dteHoy,
            DteMes = dteMes,
            TotalPagarMes = totalPagarMes,
            Procesados = procesados,
            Rechazados = rechazados,
            Contingencias = contingencias,
            Pendientes = pendientes,
            PlanNombre = plan?.Plan.Nombre,
            LimiteDteMensual = plan?.Plan.LimiteDteMensual,
            PorEstado = porEstado,
            PorTipo = porTipo,
            TendenciaDiaria = tendencia,
        };
    }

    // ─────────────────────────────────────────────────────────────
    //  Dashboard SuperAdmin
    // ─────────────────────────────────────────────────────────────

    public async Task<DashboardSuperAdminDto> GetDashboardSuperAdminAsync(CancellationToken ct = default)
    {
        var hoy = DateTime.UtcNow.Date;
        var inicioMes = new DateTime(hoy.Year, hoy.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var en30Dias = hoy.AddDays(30);

        // ── KPIs globales ─────────────────────────────────────────
        var empresasActivas = await _db.Empresas
            .CountAsync(e => e.EstadoCodigo == EstadoCodes.Activo, ct);

        var empresasTotal = await _db.Empresas.CountAsync(ct);

        var usuariosActivos = await _db.Usuarios
            .CountAsync(u => u.EstadoCodigo == EstadoCodes.Activo, ct);

        var dteTotalMes = await _db.DteDocumentos
            .CountAsync(d => d.FechaEmision >= inicioMes, ct);

        var facturacionTotalMes = await _db.DteDocumentos
            .Where(d => d.FechaEmision >= inicioMes && d.EstadoCodigo == DteEstadoCodigos.Procesado)
            .SumAsync(d => (decimal?)d.TotalPagar, ct) ?? 0m;

        // ── Top 10 empresas por DTE del mes ───────────────────────
        var topRaw = await _db.DteDocumentos
            .Where(d => d.FechaEmision >= inicioMes)
            .GroupBy(d => d.EmpresaId)
            .Select(g => new { EmpresaId = g.Key, DteCount = g.Count(), TotalPagar = g.Sum(d => d.TotalPagar) })
            .OrderByDescending(x => x.DteCount)
            .Take(10)
            .ToListAsync(ct);

        var empIds = topRaw.Select(x => x.EmpresaId).ToList();
        var empNames = await _db.Empresas
            .Where(e => empIds.Contains(e.Id))
            .Select(e => new { e.Id, e.RazonSocial })
            .ToDictionaryAsync(e => e.Id, e => e.RazonSocial, ct);

        var topEmpresas = topRaw.Select(x => new EmpresaMetricasDto
        {
            EmpresaId = x.EmpresaId,
            RazonSocial = empNames.GetValueOrDefault(x.EmpresaId, "???"),
            DteCount = x.DteCount,
            TotalPagar = x.TotalPagar,
        }).ToList();

        // ── Resumen por plan ─────────────────────────────────────
        var planesActivos = await _db.EmpresaPlanes
            .AsNoTracking()
            .Include(ep => ep.Plan)
            .Where(ep => ep.EstadoCodigo == EstadoCodes.Activo)
            .Select(ep => new { ep.Plan.Nombre, ep.Plan.PrecioMensual })
            .ToListAsync(ct);

        var resumenPorPlan = planesActivos
            .GroupBy(ep => new { ep.Nombre, ep.PrecioMensual })
            .Select(g => new PlanResumenDto
            {
                PlanNombre = g.Key.Nombre,
                EmpresasCount = g.Count(),
                IngresosMensuales = g.Key.PrecioMensual * g.Count(),
            })
            .OrderByDescending(x => x.EmpresasCount)
            .ToList();

        // ── Alertas planes próximos a vencer (≤ 30 días) ─────────
        var alertasRaw = await _db.EmpresaPlanes
            .AsNoTracking()
            .Include(ep => ep.Plan)
            .Include(ep => ep.Empresa)
            .Where(ep => ep.EstadoCodigo == EstadoCodes.Activo
                         && ep.FechaFin.HasValue
                         && ep.FechaFin.Value <= en30Dias)
            .OrderBy(ep => ep.FechaFin)
            .Take(20)
            .ToListAsync(ct);

        var alertas = alertasRaw.Select(ep => new AlertaPlanDto
        {
            EmpresaId = ep.EmpresaId,
            RazonSocial = ep.Empresa.RazonSocial,
            PlanNombre = ep.Plan.Nombre,
            FechaFin = ep.FechaFin!.Value,
            DiasRestantes = (int)Math.Max(0, (ep.FechaFin!.Value.Date - hoy).TotalDays),
        }).ToList();

        return new DashboardSuperAdminDto
        {
            EmpresasActivas = empresasActivas,
            EmpresasTotal = empresasTotal,
            UsuariosActivos = usuariosActivos,
            DteTotalMes = dteTotalMes,
            FacturacionTotalMes = facturacionTotalMes,
            ResumenPorPlan = resumenPorPlan,
            AlertasPlanProximoVencer = alertas,
            TopEmpresasDteMes = topEmpresas,
        };
    }

    // ─────────────────────────────────────────────────────────────
    //  Helpers
    // ─────────────────────────────────────────────────────────────

    private static string ResolveTipoNombre(string codigo) => codigo switch
    {
        TipoDteCodigos.FacturaConsumidorFinal    => "Factura Consumidor Final",
        TipoDteCodigos.ComprobanteCreditoFiscal  => "Comprobante Crédito Fiscal",
        TipoDteCodigos.NotaCredito               => "Nota de Crédito",
        TipoDteCodigos.NotaDebito                => "Nota de Débito",
        TipoDteCodigos.FacturaSujetoExcluido     => "Factura Sujeto Excluido",
        _                                        => $"DTE-{codigo}",
    };
}
