using Microsoft.EntityFrameworkCore;
using NeoSTP.Application.Ops;
using NeoSTP.Domain.Core.Dte;
using NeoSTP.Infrastructure.Persistence;

namespace NeoSTP.Infrastructure.Services;

/// <summary>
/// V2.5-S3 — métricas operativas del SaaS derivadas de la BD (cross-tenant, solo SuperAdmin).
/// Complementa OTel: esto responde "¿cómo está el servicio ahora?" sin collector externo.
/// </summary>
public class OperacionPanelService : IOperacionPanelService
{
    private readonly NeoStpDbContext _db;

    public OperacionPanelService(NeoStpDbContext db) => _db = db;

    public async Task<PanelOperacionDto> GetPanelAsync(CancellationToken ct = default)
    {
        var ahora = DateTime.UtcNow;
        var hace24h = ahora.AddHours(-24);
        var hace7d = ahora.AddDays(-7);

        var panel = new PanelOperacionDto
        {
            EmpresasTotal = await _db.Empresas.CountAsync(ct),
            EmpresasActivas = await _db.Empresas.CountAsync(e => e.EstadoCodigo == "ACTIVA", ct),
            Dte24h = await PeriodoAsync(hace24h, ct),
            Dte7d = await PeriodoAsync(hace7d, ct),
            AlertasActivas = await _db.Alertas.CountAsync(a => a.ResueltaAt == null, ct),
            Recordatorios7d = await _db.RecordatoriosCobro
                .CountAsync(r => r.EstadoCodigo == "ENVIADO" && r.CreatedAt >= hace7d, ct),
            PortalEnlacesActivos = await _db.PortalAccesos
                .CountAsync(p => p.RevocadoAt == null && p.ExpiraAt >= ahora, ct),
            PortalAccesos7d = await _db.PortalAccesos
                .CountAsync(p => p.UltimoAccesoAt >= hace7d, ct),
            ApiKeysActivas = await _db.ConnectApiKeys.CountAsync(k => k.Activo, ct),
        };

        panel.TopRechazos7d = await _db.DteDocumentos
            .Where(d => d.EstadoCodigo == DteEstadoCodigos.Rechazado && d.UpdatedAt >= hace7d)
            .GroupBy(d => new { d.EmpresaId, d.Empresa.RazonSocial })
            .Select(g => new EmpresaConteoDto { EmpresaId = g.Key.EmpresaId, Empresa = g.Key.RazonSocial, Conteo = g.Count() })
            .OrderByDescending(x => x.Conteo)
            .Take(5)
            .ToListAsync(ct);

        return panel;
    }

    private async Task<DtePeriodoDto> PeriodoAsync(DateTime desde, CancellationToken ct)
    {
        var q = _db.DteDocumentos.Where(d => d.EnviadoAt >= desde);
        return new DtePeriodoDto
        {
            Total = await q.CountAsync(ct),
            Procesados = await q.CountAsync(d => d.EstadoCodigo == DteEstadoCodigos.Procesado, ct),
            Rechazados = await q.CountAsync(d => d.EstadoCodigo == DteEstadoCodigos.Rechazado, ct),
            Contingencia = await q.CountAsync(d => d.EstadoCodigo == DteEstadoCodigos.Contingencia, ct),
        };
    }
}
