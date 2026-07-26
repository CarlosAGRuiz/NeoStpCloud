using Microsoft.EntityFrameworkCore;
using NeoSTP.Application.Cobranza;
using NeoSTP.Application.Common;
using NeoSTP.Application.Dashboard;
using NeoSTP.Application.Dashboard.Dtos;
using NeoSTP.Domain.Common;
using NeoSTP.Domain.Core.Cobranza;
using NeoSTP.Domain.Core.Dte;
using NeoSTP.Domain.Core.Notificaciones;
using NeoSTP.Infrastructure.Persistence;

namespace NeoSTP.Infrastructure.Services;

/// <summary>
/// Consolidado de grupo (E5): una fila por empresa donde el usuario puede operar.
/// Las métricas se calculan con consultas agrupadas sobre TODAS las empresas del
/// alcance (no una consulta por empresa), para que un contador con 30 clientes no
/// pague 120 viajes a la BD.
/// </summary>
public sealed class GrupoDashboardService : IGrupoDashboardService
{
    private static readonly string[] EstadosPendientes =
    [
        DteEstadoCodigos.Borrador, DteEstadoCodigos.Generado,
        DteEstadoCodigos.Validado, DteEstadoCodigos.Firmado, DteEstadoCodigos.Enviado,
    ];

    private readonly NeoStpDbContext _db;

    public GrupoDashboardService(NeoStpDbContext db) => _db = db;

    public async Task<Result<GrupoDashboardDto>> GetAsync(
        int userId, int? anio = null, int? mes = null, CancellationToken ct = default)
    {
        var hoy = DateTime.UtcNow.Date;
        var year = anio ?? hoy.Year;
        var month = mes ?? hoy.Month;
        if (year is < 2000 or > 2999 || month is < 1 or > 12)
            return Result<GrupoDashboardDto>.Fail("Período inválido.", "VALIDATION");

        var desde = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
        var hasta = desde.AddMonths(1);

        // ── Alcance: empresa principal + membresías activas (E1) ──────────────
        var usuario = await _db.Usuarios.AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new
            {
                u.EmpresaId,
                Nombre = u.Empresa != null ? (u.Empresa.NombreComercial ?? u.Empresa.RazonSocial) : null,
                Estado = u.Empresa != null ? u.Empresa.EstadoCodigo : null,
            })
            .FirstOrDefaultAsync(ct);
        if (usuario is null)
            return Result<GrupoDashboardDto>.Fail("Usuario no encontrado.", "USER_NOT_FOUND");

        var filas = new List<EmpresaGrupoResumenDto>();
        if (usuario.EmpresaId is int principalId)
        {
            filas.Add(new EmpresaGrupoResumenDto
            {
                EmpresaId = principalId,
                Nombre = usuario.Nombre ?? $"Empresa {principalId}",
                EsPrincipal = true,
                Activa = usuario.Estado == EmpresaEstados.Activa,
            });
        }

        var membresias = await _db.UsuarioEmpresas.AsNoTracking()
            .Where(m => m.UsuarioId == userId && m.EstadoCodigo == "ACTIVO")
            .Select(m => new
            {
                m.EmpresaId,
                Nombre = m.Empresa.NombreComercial ?? m.Empresa.RazonSocial,
                Estado = m.Empresa.EstadoCodigo,
                RolNombre = m.Rol.Nombre,
            })
            .ToListAsync(ct);

        foreach (var m in membresias)
        {
            // La principal ya está en la lista; una membresía redundante no se duplica.
            if (filas.Any(f => f.EmpresaId == m.EmpresaId)) continue;
            filas.Add(new EmpresaGrupoResumenDto
            {
                EmpresaId = m.EmpresaId,
                Nombre = m.Nombre,
                RolNombre = m.RolNombre,
                Activa = m.Estado == EmpresaEstados.Activa,
            });
        }

        var dto = new GrupoDashboardDto { Anio = year, Mes = month };
        if (filas.Count == 0) return Result<GrupoDashboardDto>.Ok(dto);

        var ids = filas.Select(f => f.EmpresaId).ToList();
        var porId = filas.ToDictionary(f => f.EmpresaId);

        // ── DTE del período, agrupado por empresa ─────────────────────────────
        var dte = await _db.DteDocumentos.AsNoTracking()
            .Where(d => ids.Contains(d.EmpresaId) && d.FechaEmision >= desde && d.FechaEmision < hasta)
            .GroupBy(d => d.EmpresaId)
            .Select(g => new
            {
                EmpresaId = g.Key,
                Total = g.Count(),
                Ventas = g.Where(d => d.EstadoCodigo == DteEstadoCodigos.Procesado).Sum(d => (decimal?)d.TotalPagar) ?? 0m,
                Iva = g.Where(d => d.EstadoCodigo == DteEstadoCodigos.Procesado).Sum(d => (decimal?)d.IvaTotal) ?? 0m,
                Rechazados = g.Count(d => d.EstadoCodigo == DteEstadoCodigos.Rechazado),
                Pendientes = g.Count(d => EstadosPendientes.Contains(d.EstadoCodigo)),
            })
            .ToListAsync(ct);

        foreach (var d in dte)
        {
            if (!porId.TryGetValue(d.EmpresaId, out var fila)) continue;
            fila.DteMes = d.Total;
            fila.VentasMes = d.Ventas;
            fila.IvaDebitoMes = d.Iva;
            fila.Rechazados = d.Rechazados;
            fila.Pendientes = d.Pendientes;
        }

        // ── Cartera (a la fecha): mismas reglas que CobranzaService ───────────
        var cobrables = await _db.DteDocumentos.AsNoTracking()
            .Where(d => ids.Contains(d.EmpresaId)
                     && d.EstadoCodigo == DteEstadoCodigos.Procesado
                     && (d.TipoDteCodigo == TipoDteCodigos.FacturaConsumidorFinal || d.TipoDteCodigo == TipoDteCodigos.ComprobanteCreditoFiscal)
                     && (d.CondicionOperacionCodigo == "2" || d.CondicionOperacionCodigo == "3"))
            .Select(d => new
            {
                d.EmpresaId,
                d.FechaEmision,
                d.PlazoDias,
                d.TotalPagar,
                Pagado = _db.Set<PagoCliente>()
                    .Where(p => p.DteDocumentoId == d.Id && p.EstadoCodigo == PagoEstados.Confirmado)
                    .Sum(p => (decimal?)p.Monto) ?? 0m,
            })
            .ToListAsync(ct);

        var hoyOnly = DateOnly.FromDateTime(hoy);
        foreach (var c in cobrables)
        {
            var saldo = CobranzaCalculator.Saldo(c.TotalPagar, c.Pagado);
            if (saldo <= 0) continue;
            if (!porId.TryGetValue(c.EmpresaId, out var fila)) continue;

            fila.PorCobrar += saldo;
            var vence = CobranzaCalculator.Vencimiento(DateOnly.FromDateTime(c.FechaEmision), c.PlazoDias);
            if (CobranzaCalculator.EstadoCobro(saldo, vence, hoyOnly) == CobroEstados.Vencido)
            {
                fila.Vencido += saldo;
                fila.FacturasVencidas++;
            }
        }

        // ── Alertas sin resolver, agrupadas por empresa ───────────────────────
        var alertas = await _db.Alertas.AsNoTracking()
            .Where(a => ids.Contains(a.EmpresaId) && a.EstadoCodigo != AlertaEstados.Resuelta)
            .GroupBy(a => a.EmpresaId)
            .Select(g => new { EmpresaId = g.Key, Total = g.Count() })
            .ToListAsync(ct);

        foreach (var a in alertas)
        {
            if (porId.TryGetValue(a.EmpresaId, out var fila)) fila.AlertasActivas = a.Total;
        }

        dto.Empresas = filas
            .OrderByDescending(f => f.VentasMes)
            .ThenBy(f => f.Nombre, StringComparer.OrdinalIgnoreCase)
            .ToList();
        return Result<GrupoDashboardDto>.Ok(dto);
    }
}
