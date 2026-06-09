using Microsoft.EntityFrameworkCore;
using NeoSTP.Application.Cobranza;
using NeoSTP.Application.Cobranza.Dtos;
using NeoSTP.Application.Notificaciones;
using NeoSTP.Application.Notificaciones.Dtos;
using NeoSTP.Domain.Core.Dte;
using NeoSTP.Domain.Core.Notificaciones;
using NeoSTP.Infrastructure.Persistence;

namespace NeoSTP.Infrastructure.Services;

/// <summary>
/// Genera alertas a partir de datos reales: DTE rechazados sin resolver, certificado de firma
/// próximo a vencer, facturas vencidas (cuentas por cobrar) y productos bajo stock mínimo.
/// Usa upsert por clave (dedupe).
/// </summary>
public class AlertaGeneracionService : IAlertaGeneracionService
{
    private const int DiasAvisoCertificado = 30;

    private readonly NeoStpDbContext _db;
    private readonly IAlertaService _alertas;
    private readonly ICobranzaService _cobranza;

    public AlertaGeneracionService(NeoStpDbContext db, IAlertaService alertas, ICobranzaService cobranza)
    {
        _db = db;
        _alertas = alertas;
        _cobranza = cobranza;
    }

    public async Task<int> GenerarAsync(int empresaId, CancellationToken ct = default)
    {
        // Claves ya existentes (no resueltas) para no duplicar ni recontar.
        var existentes = await _db.Alertas.AsNoTracking()
            .Where(a => a.EmpresaId == empresaId && a.EstadoCodigo != AlertaEstados.Resuelta)
            .Select(a => a.Clave).ToListAsync(ct);
        var set = new HashSet<string>(existentes, StringComparer.Ordinal);
        var creadas = 0;

        async Task Crear(string clave, CrearAlertaRequest req)
        {
            if (!set.Add(clave)) return;
            req.EmpresaId = empresaId;
            req.Clave = clave;
            await _alertas.CrearAsync(req, ct);
            creadas++;
        }

        // 1) DTE rechazados sin resolver
        var rechazados = await _db.DteDocumentos.AsNoTracking()
            .Where(d => d.EmpresaId == empresaId && d.EstadoCodigo == DteEstadoCodigos.Rechazado)
            .OrderByDescending(d => d.Id).Take(100)
            .Select(d => new { d.Id, d.NumeroControl })
            .ToListAsync(ct);
        foreach (var d in rechazados)
            await Crear($"{AlertaTipos.DteRechazado}:{d.Id}", new CrearAlertaRequest
            {
                TipoCodigo = AlertaTipos.DteRechazado, Severidad = AlertaSeveridades.Critica,
                Titulo = "DTE rechazado por Hacienda",
                Mensaje = $"El documento {d.NumeroControl} fue rechazado. Revisa el diagnóstico y corrige.",
                EntidadTipo = "DteDocumento", EntidadId = d.Id,
            });

        // 2) Certificado de firma próximo a vencer
        var cert = await _db.DteConfiguracion.AsNoTracking()
            .Where(c => c.EmpresaId == empresaId && c.CertificadoVence != null)
            .Select(c => c.CertificadoVence)
            .FirstOrDefaultAsync(ct);
        if (cert is DateTime vence)
        {
            var dias = (vence.Date - DateTime.UtcNow.Date).Days;
            if (dias >= 0 && dias <= DiasAvisoCertificado)
                await Crear($"{AlertaTipos.CertPorVencer}:{empresaId}", new CrearAlertaRequest
                {
                    TipoCodigo = AlertaTipos.CertPorVencer, Severidad = AlertaSeveridades.Advertencia,
                    Titulo = "Certificado de firma por vencer",
                    Mensaje = $"Tu certificado vence en {dias} día(s) ({vence:dd/MM/yyyy}). Renuévalo para seguir emitiendo.",
                    EntidadTipo = "DteConfiguracion", EntidadId = empresaId,
                });
        }

        // 3) Facturas vencidas (cuentas por cobrar)
        var vencidas = await _cobranza.GetPendientesAsync(empresaId, new CobranzaQuery { SoloVencidas = true, PageSize = 50 }, ct);
        if (vencidas.IsSuccess)
        {
            foreach (var f in vencidas.Value!.Items)
                await Crear($"{AlertaTipos.FacturaVencida}:{f.DteDocumentoId}", new CrearAlertaRequest
                {
                    TipoCodigo = AlertaTipos.FacturaVencida, Severidad = AlertaSeveridades.Advertencia,
                    Titulo = "Factura vencida sin cobrar",
                    Mensaje = $"{f.ClienteNombre}: {f.NumeroControl} vencida hace {f.DiasVencido} día(s), saldo $ {f.Saldo:N2}.",
                    EntidadTipo = "DteDocumento", EntidadId = f.DteDocumentoId,
                });
        }

        // 4) Productos bajo stock mínimo (inventario)
        var bajoStock = await (from e in _db.ExistenciasProducto.AsNoTracking()
                               join p in _db.Productos.AsNoTracking() on new { e.EmpresaId, e.ProductoId } equals new { p.EmpresaId, ProductoId = p.Id }
                               where e.EmpresaId == empresaId && e.StockMinimo > 0 && e.Cantidad <= e.StockMinimo && p.EstadoCodigo == "ACTIVO"
                               orderby e.Cantidad
                               select new { p.Id, p.CodigoInterno, p.Nombre, e.Cantidad, e.StockMinimo })
                              .Take(100).ToListAsync(ct);
        foreach (var s in bajoStock)
            await Crear($"{AlertaTipos.StockBajo}:{s.Id}", new CrearAlertaRequest
            {
                TipoCodigo = AlertaTipos.StockBajo, Severidad = AlertaSeveridades.Advertencia,
                Titulo = "Producto bajo stock mínimo",
                Mensaje = $"{s.CodigoInterno} · {s.Nombre}: quedan {s.Cantidad:N2} (mínimo {s.StockMinimo:N2}). Considera reabastecer.",
                EntidadTipo = "Producto", EntidadId = s.Id,
            });

        return creadas;
    }
}
