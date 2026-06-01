using Microsoft.EntityFrameworkCore;
using NeoSTP.Application.Common;
using NeoSTP.Application.Dte.Diagnostico;
using NeoSTP.Application.Dte.Diagnostico.Dtos;
using NeoSTP.Domain.Core.Dte.Diagnostico;
using NeoSTP.Infrastructure.Persistence;

namespace NeoSTP.Infrastructure.Services;

public class DiagnosticoHaciendaService : IDiagnosticoHaciendaService
{
    private readonly NeoStpDbContext _db;

    public DiagnosticoHaciendaService(NeoStpDbContext db) => _db = db;

    // ──────────────────────────────────────────────────────────────────────────
    public async Task<DiagnosticoResumenDto> ObtenerResumenAsync(int empresaId, CancellationToken ct = default)
    {
        var q = _db.DteErrorOcurrencias.AsNoTracking().Where(o => o.EmpresaId == empresaId);
        var hoy = DateTime.UtcNow.Date;

        var total = await q.CountAsync(ct);
        var noResueltos = await q.CountAsync(o => !o.Resuelta, ct);
        var erroresHoy = await q.CountAsync(o => o.OcurrioAt >= hoy, ct);

        var topCodigos = await q
            .GroupBy(o => o.CodigoError)
            .Select(g => new { Codigo = g.Key, Cantidad = g.Count() })
            .OrderByDescending(x => x.Cantidad)
            .Take(10)
            .ToListAsync(ct);

        var catalogoMap = await ObtenerCatalogoMapAsync(ct);

        var topDto = topCodigos.Select(x => new ErrorPorCodigoDto(
            x.Codigo, x.Cantidad,
            catalogoMap.TryGetValue(x.Codigo, out var cat) ? cat.Descripcion : null
        )).ToList();

        return new DiagnosticoResumenDto(total, noResueltos, erroresHoy, topDto);
    }

    // ──────────────────────────────────────────────────────────────────────────
    public async Task<PagedResult<ErrorOcurrenciaListItemDto>> ListarOcurrenciasAsync(
        int empresaId, DiagnosticoFiltroRequest filtro, CancellationToken ct = default)
    {
        var q = _db.DteErrorOcurrencias.AsNoTracking().Where(o => o.EmpresaId == empresaId);

        if (!string.IsNullOrWhiteSpace(filtro.CodigoError))
            q = q.Where(o => o.CodigoError == filtro.CodigoError.Trim());
        if (!string.IsNullOrWhiteSpace(filtro.Fuente))
            q = q.Where(o => o.Fuente == filtro.Fuente.Trim());
        if (filtro.Desde is not null)
            q = q.Where(o => o.OcurrioAt >= filtro.Desde.Value);
        if (filtro.Hasta is not null)
            q = q.Where(o => o.OcurrioAt <= filtro.Hasta.Value);
        if (filtro.SoloNoResueltas == true)
            q = q.Where(o => !o.Resuelta);

        var total = await q.CountAsync(ct);
        var page = Math.Max(1, filtro.Page);
        var pageSize = Math.Clamp(filtro.PageSize, 1, 100);

        var items = await q.OrderByDescending(o => o.OcurrioAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync(ct);

        var catalogoMap = await ObtenerCatalogoMapAsync(ct);

        var dtos = items.Select(o => MapOcurrencia(o, catalogoMap)).ToList();
        return PagedResult<ErrorOcurrenciaListItemDto>.Create(dtos, total, page, pageSize);
    }

    // ──────────────────────────────────────────────────────────────────────────
    public async Task<DiagnosticoDocumentoDto?> ObtenerDiagnosticoDocumentoAsync(
        int empresaId, int dteDocumentoId, CancellationToken ct = default)
    {
        var doc = await _db.DteDocumentos.AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == dteDocumentoId && d.EmpresaId == empresaId, ct);
        if (doc is null) return null;

        var json = await _db.DteDocumentoJson.AsNoTracking()
            .FirstOrDefaultAsync(j => j.DocumentoId == dteDocumentoId, ct);

        var errores = await _db.DteErrorOcurrencias.AsNoTracking()
            .Where(o => o.EmpresaId == empresaId && o.DteDocumentoId == dteDocumentoId)
            .OrderByDescending(o => o.OcurrioAt)
            .ToListAsync(ct);

        var catalogoMap = await ObtenerCatalogoMapAsync(ct);

        return new DiagnosticoDocumentoDto(
            doc.Id, doc.CodigoGeneracion, doc.NumeroControl, doc.TipoDteCodigo, doc.EstadoCodigo,
            json?.JsonFirmado ?? json?.JsonDte,
            json?.RespuestaHacienda,
            errores.Select(o => MapOcurrencia(o, catalogoMap)).ToList()
        );
    }

    // ──────────────────────────────────────────────────────────────────────────
    public async Task<DiagnosticoEventoDto?> ObtenerDiagnosticoEventoAsync(
        int empresaId, int dteEventoId, CancellationToken ct = default)
    {
        var evento = await _db.DteEventos.AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == dteEventoId && e.EmpresaId == empresaId, ct);
        if (evento is null) return null;

        var ultimaRespuesta = await _db.DteEventoRespuestas.AsNoTracking()
            .Where(r => r.EventoId == dteEventoId)
            .OrderByDescending(r => r.RecibidoAt)
            .FirstOrDefaultAsync(ct);

        var errores = await _db.DteErrorOcurrencias.AsNoTracking()
            .Where(o => o.EmpresaId == empresaId && o.DteEventoId == dteEventoId)
            .OrderByDescending(o => o.OcurrioAt)
            .ToListAsync(ct);

        var catalogoMap = await ObtenerCatalogoMapAsync(ct);

        return new DiagnosticoEventoDto(
            evento.Id, evento.TipoEventoCodigo, evento.EstadoCodigo,
            ultimaRespuesta?.RespuestaCrudaJson,
            errores.Select(o => MapOcurrencia(o, catalogoMap)).ToList()
        );
    }

    // ──────────────────────────────────────────────────────────────────────────
    public async Task<IReadOnlyList<ErrorCatalogoDto>> ListarCatalogoAsync(CancellationToken ct = default)
    {
        var items = await _db.DteErrorCatalogo.AsNoTracking()
            .Where(c => c.Activo)
            .OrderBy(c => c.Tipo).ThenBy(c => c.Codigo)
            .ToListAsync(ct);

        return items.Select(c => new ErrorCatalogoDto(
            c.Codigo, c.Tipo, c.MensajeTecnico, c.Descripcion,
            c.CausaProbable, c.AccionSugerida, c.Severidad
        )).ToList();
    }

    // ──────────────────────────────────────────────────────────────────────────
    public async Task<Result<string>> MarcarResueltaAsync(
        int empresaId, int ocurrenciaId, string actor, CancellationToken ct = default)
    {
        var ocurrencia = await _db.DteErrorOcurrencias
            .FirstOrDefaultAsync(o => o.Id == ocurrenciaId && o.EmpresaId == empresaId, ct);
        if (ocurrencia is null)
            return Result<string>.Fail("Ocurrencia no encontrada.", "OCURRENCIA_NOT_FOUND");

        ocurrencia.Resuelta = true;
        ocurrencia.UpdatedAt = DateTime.UtcNow;
        ocurrencia.UpdatedBy = actor;
        await _db.SaveChangesAsync(ct);
        return Result<string>.Ok("Ocurrencia marcada como resuelta.");
    }

    // ──────────────────────────────────────────────────────────────────────────
    public async Task<int> SincronizarOcurrenciasAsync(int empresaId, CancellationToken ct = default)
    {
        int importadas = 0;

        // 1. Errores de certificación
        var erroresCertif = await _db.CertificacionErrores.AsNoTracking()
            .Include(e => e.Prueba).ThenInclude(p => p.Escenario)
            .Where(e => e.Prueba.EmpresaId == empresaId)
            .ToListAsync(ct);

        foreach (var err in erroresCertif)
        {
            var existe = await _db.DteErrorOcurrencias.AnyAsync(
                o => o.EmpresaId == empresaId
                  && o.Fuente == DteErrorFuente.Certificacion
                  && o.DteDocumentoId == err.Prueba.DteDocumentoId
                  && o.CodigoError == err.CodigoMh
                  && o.OcurrioAt == err.OcurrioAt, ct);

            if (!existe)
            {
                _db.DteErrorOcurrencias.Add(new DteErrorOcurrencia
                {
                    EmpresaId = empresaId,
                    CodigoError = err.CodigoMh,
                    Mensaje = err.Descripcion,
                    RespuestaMhJson = err.RespuestaMhJson,
                    Fuente = DteErrorFuente.Certificacion,
                    DteDocumentoId = err.Prueba.DteDocumentoId,
                    OcurrioAt = err.OcurrioAt,
                    CreatedAt = DateTime.UtcNow,
                });
                importadas++;
            }
        }

        // 2. Respuestas de eventos rechazadas
        var respuestasEvento = await _db.DteEventoRespuestas.AsNoTracking()
            .Include(r => r.Evento)
            .Where(r => r.Evento.EmpresaId == empresaId
                     && r.Estado != null && r.Estado != "PROCESADO"
                     && r.CodigoMsg != null)
            .ToListAsync(ct);

        foreach (var resp in respuestasEvento)
        {
            var existe = await _db.DteErrorOcurrencias.AnyAsync(
                o => o.EmpresaId == empresaId
                  && o.Fuente == DteErrorFuente.Evento
                  && o.DteEventoId == resp.EventoId
                  && o.CodigoError == resp.CodigoMsg
                  && o.OcurrioAt == resp.RecibidoAt, ct);

            if (!existe)
            {
                _db.DteErrorOcurrencias.Add(new DteErrorOcurrencia
                {
                    EmpresaId = empresaId,
                    CodigoError = resp.CodigoMsg!,
                    Mensaje = resp.DescripcionMsg ?? resp.Estado ?? "Sin descripción",
                    RespuestaMhJson = resp.RespuestaCrudaJson,
                    Fuente = DteErrorFuente.Evento,
                    DteEventoId = resp.EventoId,
                    OcurrioAt = resp.RecibidoAt,
                    CreatedAt = DateTime.UtcNow,
                });
                importadas++;
            }
        }

        if (importadas > 0)
            await _db.SaveChangesAsync(ct);

        return importadas;
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────────────────────

    private async Task<Dictionary<string, DteErrorCatalogo>> ObtenerCatalogoMapAsync(CancellationToken ct)
        => await _db.DteErrorCatalogo.AsNoTracking().Where(c => c.Activo)
            .ToDictionaryAsync(c => c.Codigo, ct);

    private static ErrorOcurrenciaListItemDto MapOcurrencia(
        DteErrorOcurrencia o, Dictionary<string, DteErrorCatalogo> catalogo)
    {
        catalogo.TryGetValue(o.CodigoError, out var cat);
        return new ErrorOcurrenciaListItemDto(
            o.Id, o.CodigoError, o.Mensaje, o.Fuente,
            o.DteDocumentoId, o.DteEventoId, o.OcurrioAt, o.Resuelta,
            cat?.Descripcion, cat?.CausaProbable, cat?.AccionSugerida, cat?.Severidad
        );
    }
}
