using Microsoft.EntityFrameworkCore;
using NeoSTP.Application.Common;
using NeoSTP.Application.Dte;
using NeoSTP.Application.Dte.Eventos;
using NeoSTP.Application.Dte.Eventos.Dtos;
using NeoSTP.Infrastructure.Persistence;

namespace NeoSTP.Infrastructure.Services;

/// <summary>
/// Sprint 15 — consulta y creación de eventos DTE persistidos.
///
/// La transmisión real (firma RS512 + envío a Hacienda) vive en
/// <see cref="IDteDocumentosService"/> y es la que persiste el evento. Este
/// servicio orquesta la creación delegando en esos métodos y expone la lectura
/// sobre las tablas Dte_Eventos*.
/// </summary>
public class DteEventoService : IDteEventoService
{
    private readonly NeoStpDbContext _db;
    private readonly IDteDocumentosService _documentos;

    public DteEventoService(NeoStpDbContext db, IDteDocumentosService documentos)
    {
        _db = db;
        _documentos = documentos;
    }

    // -------------------------------------------------------- Lectura

    public async Task<Result<IReadOnlyList<DteEventoListDto>>> GetListAsync(int empresaId, string? tipoEvento = null, string? estado = null, CancellationToken ct = default)
    {
        var q = _db.DteEventos.AsNoTracking().Where(e => e.EmpresaId == empresaId);

        if (!string.IsNullOrWhiteSpace(tipoEvento))
        {
            var t = tipoEvento.Trim().ToUpperInvariant();
            q = q.Where(e => e.TipoEventoCodigo == t);
        }
        if (!string.IsNullOrWhiteSpace(estado))
        {
            var s = estado.Trim().ToUpperInvariant();
            q = q.Where(e => e.EstadoCodigo == s);
        }

        var items = await q
            .OrderByDescending(e => e.FechaTransmision)
            .Take(500)
            .Select(e => new DteEventoListDto
            {
                Id = e.Id,
                TipoEventoCodigo = e.TipoEventoCodigo,
                CodigoGeneracion = e.CodigoGeneracion,
                EstadoCodigo = e.EstadoCodigo,
                AmbienteCodigo = e.AmbienteCodigo,
                FechaTransmision = e.FechaTransmision,
                SelloRecibido = e.SelloRecibido,
                DocumentosRelacionados = e.DocumentosRelacionados.Count,
            })
            .ToListAsync(ct);

        return Result<IReadOnlyList<DteEventoListDto>>.Ok(items);
    }

    public async Task<Result<DteEventoDetalleDto>> GetByIdAsync(int empresaId, int id, CancellationToken ct = default)
    {
        var e = await _db.DteEventos.AsNoTracking()
            .Include(x => x.Json)
            .Include(x => x.Respuestas)
            .Include(x => x.DocumentosRelacionados).ThenInclude(d => d.Documento)
            .FirstOrDefaultAsync(x => x.Id == id && x.EmpresaId == empresaId, ct);
        if (e is null) return Result<DteEventoDetalleDto>.Fail("Evento no encontrado.", "EVENTO_NOT_FOUND");

        var dto = new DteEventoDetalleDto
        {
            Id = e.Id,
            EmpresaId = e.EmpresaId,
            TipoEventoCodigo = e.TipoEventoCodigo,
            CodigoGeneracion = e.CodigoGeneracion,
            Version = e.Version,
            AmbienteCodigo = e.AmbienteCodigo,
            EstadoCodigo = e.EstadoCodigo,
            FechaTransmision = e.FechaTransmision,
            SelloRecibido = e.SelloRecibido,
            NumeroControlReferencia = e.NumeroControlReferencia,
            MotivoLibre = e.MotivoLibre,
            FinalizadoAt = e.FinalizadoAt,
            TieneJson = e.Json != null,
            TieneJws = e.Json != null && !string.IsNullOrEmpty(e.Json.JwsFirmado),
            Relacionados = e.DocumentosRelacionados
                .Select(d => new DteEventoRelacionadoDto
                {
                    DocumentoId = d.DocumentoId,
                    RolCodigo = d.RolCodigo,
                    NumeroControl = d.NumeroControlSnapshot ?? d.Documento.NumeroControl,
                    TipoDteCodigo = d.Documento.TipoDteCodigo,
                })
                .ToList(),
            Respuestas = e.Respuestas
                .OrderByDescending(r => r.RecibidoAt)
                .Select(r => new DteEventoRespuestaDto
                {
                    Id = r.Id,
                    Estado = r.Estado,
                    CodigoMsg = r.CodigoMsg,
                    DescripcionMsg = r.DescripcionMsg,
                    SelloRecibido = r.SelloRecibido,
                    RespuestaCrudaJson = r.RespuestaCrudaJson,
                    RecibidoAt = r.RecibidoAt,
                })
                .ToList(),
        };
        return Result<DteEventoDetalleDto>.Ok(dto);
    }

    public async Task<Result<DteEventoJsonDto>> GetJsonAsync(int empresaId, int id, CancellationToken ct = default)
    {
        var json = await _db.DteEventos.AsNoTracking()
            .Where(e => e.Id == id && e.EmpresaId == empresaId && e.Json != null)
            .Select(e => new DteEventoJsonDto
            {
                EventoId = e.Id,
                JsonSinFirmar = e.Json!.JsonSinFirmar,
                JwsFirmado = e.Json.JwsFirmado,
            })
            .FirstOrDefaultAsync(ct);

        return json is null
            ? Result<DteEventoJsonDto>.Fail("Evento o JSON no encontrado.", "EVENTO_NOT_FOUND")
            : Result<DteEventoJsonDto>.Ok(json);
    }

    // -------------------------------------------------------- Creación (delegada)

    public Task<Result<CrearEventoResultadoDto>> CrearInvalidacionAsync(int empresaId, CrearEventoInvalidacionRequest r, string? actor, CancellationToken ct = default)
        => _documentos.TransmitirInvalidacionEventoAsync(empresaId, r.DocumentoId, r.TipoAnulacion, r.MotivoAnulacion,
            r.CodigoGeneracionReemplazo, r.NombreResponsable, r.TipoDocResponsable, r.NumDocResponsable, actor, ct);

    public Task<Result<CrearEventoResultadoDto>> CrearContingenciaAsync(int empresaId, CrearEventoContingenciaRequest r, string? actor, CancellationToken ct = default)
        => _documentos.TransmitirEventoContingenciaAsync(empresaId, r.DocumentoIds, r.TipoContingencia, r.Motivo,
            r.NombreResponsable, r.TipoDocResponsable, r.NumeroDocResponsable, actor, ct);

    public Task<Result<CrearEventoResultadoDto>> CrearRetornoAsync(int empresaId, CrearEventoRetornoRequest r, string? actor, CancellationToken ct = default)
        => _documentos.TransmitirEventoRetornoAsync(empresaId, r.DocumentoOrigenId, actor, ct);

    public Task<Result<CrearEventoResultadoDto>> CrearOperacionesEspecialesAsync(int empresaId, CrearEventoOperacionesEspecialesRequest r, string? actor, CancellationToken ct = default)
        => _documentos.TransmitirEventoOperacionesEspecialesAsync(empresaId, r.CodigoGeneracionRef, r.Descripcion, r.Monto, actor, ct);
}
