using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Common;
using NeoSTP.Application.Dte.Abstractions;
using NeoSTP.Application.Dte.Contingencia;
using NeoSTP.Application.Dte.Contingencia.Dtos;
using NeoSTP.Domain.Core.Dte;
using NeoSTP.Domain.Core.Dte.Contingencia;
using NeoSTP.Domain.Core.Dte.Eventos;
using NeoSTP.Infrastructure.Persistence;

namespace NeoSTP.Infrastructure.Services;

/// <summary>
/// Implementa el MOMENTO 3 del ciclo de contingencia:
///
///   MOMENTO 1 — Emitir DTE con tipoTransmision=2 → estado CONTINGENCIA    (sprint anterior)
///   MOMENTO 2 — Enviar Evento de Contingencia → obtener sello del evento   (sprint anterior)
///   MOMENTO 3 — Enviar lote y consultar sellos individuales                ← ESTE SERVICIO
///
/// Un lote se crea por cada DteEvento de tipo CONTINGENCIA con sello
/// (EstadoCodigo = PROCESADO). El lote se envía vía POST /fesv/recepcionlote
/// y luego se consulta vía GET /fesv/recepcion/consultadtelote/{codigoLote}
/// para obtener el sello individual de cada DTE.
/// </summary>
public class ContingenciaLoteService : IContingenciaLoteService
{
    private readonly NeoStpDbContext _db;
    private readonly IHaciendaLoteClient _loteClient;
    private readonly IHaciendaConsultaLoteClient _consultaClient;
    private readonly IHaciendaAuthClient _haciendaAuth;
    private readonly ISecretProtector _protector;
    private readonly ILogger<ContingenciaLoteService> _logger;

    public ContingenciaLoteService(
        NeoStpDbContext db,
        IHaciendaLoteClient loteClient,
        IHaciendaConsultaLoteClient consultaClient,
        IHaciendaAuthClient haciendaAuth,
        ISecretProtector protector,
        ILogger<ContingenciaLoteService> logger)
    {
        _db = db;
        _loteClient = loteClient;
        _consultaClient = consultaClient;
        _haciendaAuth = haciendaAuth;
        _protector = protector;
        _logger = logger;
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Queries
    // ──────────────────────────────────────────────────────────────────────────

    public async Task<ContingenciaResumenDto> ObtenerResumenAsync(int empresaId, CancellationToken ct = default)
    {
        var pendientes = await _db.DteDocumentos
            .Where(d => d.EmpresaId == empresaId && d.EstadoCodigo == DteEstadoCodigos.Contingencia)
            .CountAsync(ct);

        var lotesPendientes = await _db.DteContingenciaLotes
            .Where(l => l.EmpresaId == empresaId
                     && (l.EstadoCodigo == DteContingenciaLoteEstados.Pendiente
                      || l.EstadoCodigo == DteContingenciaLoteEstados.Enviado))
            .CountAsync(ct);

        var procesados = await _db.DteContingenciaLoteDetalles
            .Where(d => d.Lote.EmpresaId == empresaId
                     && d.EstadoCodigo == DteContingenciaLoteEstados.Procesado)
            .CountAsync(ct);

        var conError = await _db.DteContingenciaLoteDetalles
            .Where(d => d.Lote.EmpresaId == empresaId
                     && d.EstadoCodigo == DteContingenciaLoteEstados.Error)
            .CountAsync(ct);

        var loteAntiguo = await _db.DteContingenciaLotes
            .Where(l => l.EmpresaId == empresaId
                     && (l.EstadoCodigo == DteContingenciaLoteEstados.Pendiente
                      || l.EstadoCodigo == DteContingenciaLoteEstados.Enviado))
            .OrderBy(l => l.CreatedAt)
            .Select(l => (DateTime?)l.CreatedAt)
            .FirstOrDefaultAsync(ct);

        var lotesEventos = await _db.DteContingenciaLotes
            .Where(l => l.EmpresaId == empresaId)
            .Select(l => l.EventoContingenciaId)
            .ToListAsync(ct);

        var eventoSinLote = await _db.DteEventos
            .Where(e => e.EmpresaId == empresaId
                     && e.TipoEventoCodigo == TipoEventoCodigos.Contingencia
                     && e.EstadoCodigo == DteEventoEstadoCodigos.Procesado
                     && !string.IsNullOrEmpty(e.SelloRecibido)
                     && !lotesEventos.Contains(e.Id))
            .OrderByDescending(e => e.FinalizadoAt)
            .Select(e => (int?)e.Id)
            .FirstOrDefaultAsync(ct);

        return new ContingenciaResumenDto
        {
            DocumentosPendientes = pendientes,
            LotesPendientes = lotesPendientes,
            DocumentosProcesados = procesados,
            DocumentosConError = conError,
            VencimientoLoteMasAntiguo = loteAntiguo.HasValue ? loteAntiguo.Value.AddHours(72) : null,
            EventoSinLoteId = eventoSinLote,
        };
    }

    public async Task<IReadOnlyList<ContingenciaDocumentoDto>> ListarDocumentosPendientesAsync(int empresaId, CancellationToken ct = default)
    {
        var ahora = DateTime.UtcNow;
        return await _db.DteDocumentos
            .Where(d => d.EmpresaId == empresaId && d.EstadoCodigo == DteEstadoCodigos.Contingencia)
            .OrderBy(d => d.CreatedAt)
            .Select(d => new ContingenciaDocumentoDto
            {
                Id = d.Id,
                TipoDteCodigo = d.TipoDteCodigo,
                NumeroControl = d.NumeroControl,
                CodigoGeneracion = d.CodigoGeneracion,
                FechaEmision = d.FechaEmision,
                ReceptorNombre = d.ReceptorNombre,
                TotalPagar = d.TotalPagar,
                EstadoCodigo = d.EstadoCodigo,
                IntentoRetransmision = d.IntentoRetransmision,
                UltimoIntentoAt = d.UltimoIntentoRetransmisionAt,
                HorasDesdeEmision = EF.Functions.DateDiffHour(d.FechaEmision, ahora),
            })
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<ContingenciaLoteListItemDto>> ListarLotesAsync(int empresaId, CancellationToken ct = default)
    {
        return await _db.DteContingenciaLotes
            .Where(l => l.EmpresaId == empresaId)
            .OrderByDescending(l => l.CreatedAt)
            .Select(l => new ContingenciaLoteListItemDto
            {
                Id = l.Id,
                EventoContingenciaId = l.EventoContingenciaId,
                CodigoLote = l.CodigoLote,
                SelloRecibido = l.SelloRecibido,
                EstadoCodigo = l.EstadoCodigo,
                AmbienteCodigo = l.AmbienteCodigo,
                TotalDte = l.Detalles.Count,
                DteProcesados = l.Detalles.Count(d => d.EstadoCodigo == DteContingenciaLoteEstados.Procesado),
                EnviadoAt = l.EnviadoAt,
                UltimaConsultaAt = l.UltimaConsultaAt,
                Intentos = l.Intentos,
                CreatedAt = l.CreatedAt,
            })
            .ToListAsync(ct);
    }

    public async Task<ContingenciaLoteDto?> ObtenerLoteAsync(int loteId, int empresaId, CancellationToken ct = default)
    {
        var lote = await _db.DteContingenciaLotes
            .Include(l => l.Detalles)
            .FirstOrDefaultAsync(l => l.Id == loteId && l.EmpresaId == empresaId, ct);

        if (lote is null) return null;

        return MapLoteDto(lote);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Comandos
    // ──────────────────────────────────────────────────────────────────────────

    public async Task<Result<CrearLoteResultadoDto>> CrearYEnviarLoteAsync(
        int eventoContingenciaId, int empresaId, string actor, CancellationToken ct = default)
    {
        // Idempotencia: si ya existe un lote para este evento devolver el existente
        var loteExistente = await _db.DteContingenciaLotes
            .FirstOrDefaultAsync(l => l.EventoContingenciaId == eventoContingenciaId
                                   && l.EmpresaId == empresaId, ct);
        if (loteExistente is not null)
        {
            return Result<CrearLoteResultadoDto>.Ok(new CrearLoteResultadoDto
            {
                LoteId = loteExistente.Id,
                EstadoCodigo = loteExistente.EstadoCodigo,
                CodigoLote = loteExistente.CodigoLote,
                SelloRecibido = loteExistente.SelloRecibido,
                Mensaje = "El lote ya existe para este evento.",
            });
        }

        var evento = await _db.DteEventos
            .FirstOrDefaultAsync(e => e.Id == eventoContingenciaId
                                   && e.EmpresaId == empresaId
                                   && e.TipoEventoCodigo == TipoEventoCodigos.Contingencia, ct);
        if (evento is null)
            return Result<CrearLoteResultadoDto>.Fail("Evento de contingencia no encontrado.", "EVENTO_NOT_FOUND");

        if (evento.EstadoCodigo != DteEventoEstadoCodigos.Procesado || string.IsNullOrEmpty(evento.SelloRecibido))
            return Result<CrearLoteResultadoDto>.Fail("El evento debe estar PROCESADO con sello.", "EVENTO_SIN_SELLO");

        var relacionados = await _db.DteEventoDocumentosRelacionados
            .Where(r => r.EventoId == eventoContingenciaId)
            .ToListAsync(ct);
        if (relacionados.Count == 0)
            return Result<CrearLoteResultadoDto>.Fail("El evento no tiene DTE relacionados.", "SIN_DTE_RELACIONADOS");

        var documentoIds = relacionados.Select(r => r.DocumentoId).ToHashSet();
        var documentos = await _db.DteDocumentos
            .Include(d => d.Json)
            .Where(d => d.EmpresaId == empresaId && documentoIds.Contains(d.Id))
            .ToListAsync(ct);

        var config = await _db.DteConfiguracion
            .FirstOrDefaultAsync(c => c.EmpresaId == empresaId, ct);
        if (config is null)
            return Result<CrearLoteResultadoDto>.Fail("Configuración DTE no encontrada.", "CONFIG_NOT_FOUND");

        var empresa = await _db.Empresas
            .FirstOrDefaultAsync(e => e.Id == empresaId, ct);
        if (empresa is null)
            return Result<CrearLoteResultadoDto>.Fail("Empresa no encontrada.", "EMPRESA_NOT_FOUND");

        var tokenResult = await ObtenerTokenAsync(config, ct);
        if (!tokenResult.Success)
            return Result<CrearLoteResultadoDto>.Fail(tokenResult.Mensaje ?? "No se pudo obtener token.", "HACIENDA_AUTH_FAILED");

        // Construir items del lote (solo DTE con JWS firmado)
        var items = new List<HaciendaLoteItem>();
        foreach (var doc in documentos)
        {
            var jws = doc.Json?.JsonFirmado;
            if (string.IsNullOrEmpty(jws))
            {
                _logger.LogWarning("ContingenciaLoteService: DTE {CodigoGen} sin JWS; omitido del lote.", doc.CodigoGeneracion);
                continue;
            }
            items.Add(new HaciendaLoteItem
            {
                TipoDte = doc.TipoDteCodigo,
                CodigoGeneracion = doc.CodigoGeneracion,
                Documento = jws,
            });
        }

        if (items.Count == 0)
            return Result<CrearLoteResultadoDto>.Fail("Ningún DTE tiene JWS firmado disponible.", "SIN_JWS_DISPONIBLE");

        // Persistir el lote antes de enviarlo
        var lote = new DteContingenciaLote
        {
            EmpresaId = empresaId,
            EventoContingenciaId = eventoContingenciaId,
            EstadoCodigo = DteContingenciaLoteEstados.Pendiente,
            AmbienteCodigo = config.AmbienteCodigo,
            CreatedBy = actor,
            UpdatedBy = actor,
            Detalles = documentos.Select(d => new DteContingenciaLoteDetalle
            {
                DteDocumentoId = d.Id,
                CodigoGeneracion = d.CodigoGeneracion,
                TipoDteCodigo = d.TipoDteCodigo,
                EstadoCodigo = DteContingenciaLoteEstados.Pendiente,
                CreatedBy = actor,
                UpdatedBy = actor,
            }).ToList(),
        };
        _db.DteContingenciaLotes.Add(lote);
        await _db.SaveChangesAsync(ct);

        // Enviar a Hacienda
        lote.Intentos++;
        var envio = await _loteClient.EnviarLoteAsync(new HaciendaLoteRequest
        {
            Ambiente = config.AmbienteCodigo == "PRODUCCION" ? "01" : "00",
            AmbienteCodigo = config.AmbienteCodigo,
            Nit = empresa.Nit!,
            SelloEvento = evento.SelloRecibido!,
            Items = items,
            Token = tokenResult.Token!,
        }, ct);

        lote.RawEnvio = envio.Raw;
        lote.EnviadoAt = DateTime.UtcNow;
        lote.UpdatedBy = actor;

        if (!envio.Success)
        {
            lote.EstadoCodigo = DteContingenciaLoteEstados.Error;
            await _db.SaveChangesAsync(ct);
            return Result<CrearLoteResultadoDto>.Fail(
                envio.DescripcionMsg ?? "Error enviando lote.",
                envio.CodigoMsg ?? "LOTE_ENVIO_FAILED");
        }

        lote.CodigoLote = envio.CodigoLote;
        lote.SelloRecibido = envio.SelloRecibido;
        lote.EstadoCodigo = DteContingenciaLoteEstados.Enviado;
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "ContingenciaLoteService: lote {LoteId} enviado. CodigoLote={CodigoLote}",
            lote.Id, lote.CodigoLote);

        return Result<CrearLoteResultadoDto>.Ok(new CrearLoteResultadoDto
        {
            LoteId = lote.Id,
            EstadoCodigo = lote.EstadoCodigo,
            CodigoLote = lote.CodigoLote,
            SelloRecibido = lote.SelloRecibido,
            Mensaje = "Lote enviado correctamente.",
        });
    }

    public async Task<Result<ConsultarLoteResultadoDto>> ConsultarLoteAsync(
        int loteId, int empresaId, CancellationToken ct = default)
    {
        var lote = await _db.DteContingenciaLotes
            .Include(l => l.Detalles)
            .FirstOrDefaultAsync(l => l.Id == loteId && l.EmpresaId == empresaId, ct);
        if (lote is null)
            return Result<ConsultarLoteResultadoDto>.Fail("Lote no encontrado.", "LOTE_NOT_FOUND");

        if (string.IsNullOrEmpty(lote.CodigoLote))
            return Result<ConsultarLoteResultadoDto>.Fail("El lote no tiene código asignado.", "SIN_CODIGO_LOTE");

        var config = await _db.DteConfiguracion
            .FirstOrDefaultAsync(c => c.EmpresaId == empresaId, ct);
        if (config is null)
            return Result<ConsultarLoteResultadoDto>.Fail("Configuración DTE no encontrada.", "CONFIG_NOT_FOUND");

        var tokenResult = await ObtenerTokenAsync(config, ct);
        if (!tokenResult.Success)
            return Result<ConsultarLoteResultadoDto>.Fail(
                tokenResult.Mensaje ?? "No se pudo obtener token.", "HACIENDA_AUTH_FAILED");

        var consulta = await _consultaClient.ConsultarLoteAsync(new HaciendaConsultaLoteRequest
        {
            CodigoLote = lote.CodigoLote,
            AmbienteCodigo = config.AmbienteCodigo,
            Token = tokenResult.Token!,
        }, ct);

        lote.UltimaConsultaAt = DateTime.UtcNow;
        lote.RawConsulta = consulta.Raw;

        if (!consulta.Success)
        {
            lote.EstadoCodigo = DteContingenciaLoteEstados.Error;
            await _db.SaveChangesAsync(ct);
            return Result<ConsultarLoteResultadoDto>.Fail(
                consulta.DescripcionMsg ?? "Error consultando lote.", "LOTE_CONSULTA_FAILED");
        }

        // Actualizar sello individual de cada DTE
        foreach (var item in consulta.Items)
        {
            var detalle = lote.Detalles.FirstOrDefault(d => d.CodigoGeneracion == item.CodigoGeneracion);
            if (detalle is null) continue;

            detalle.SelloRecibido = item.SelloRecibido;
            detalle.MensajeHacienda = item.DescripcionMsg;
            detalle.EstadoCodigo = !string.IsNullOrEmpty(item.SelloRecibido)
                ? DteContingenciaLoteEstados.Procesado
                : DteContingenciaLoteEstados.Error;

            if (!string.IsNullOrEmpty(item.SelloRecibido))
            {
                var dte = await _db.DteDocumentos
                    .FirstOrDefaultAsync(d => d.CodigoGeneracion == item.CodigoGeneracion
                                           && d.EmpresaId == empresaId, ct);
                if (dte is not null)
                {
                    dte.SelloRecibido = item.SelloRecibido;
                    dte.EstadoCodigo = DteEstadoCodigos.Procesado;
                }
            }
        }

        var todosTerminados = lote.Detalles.All(d =>
            d.EstadoCodigo == DteContingenciaLoteEstados.Procesado
         || d.EstadoCodigo == DteContingenciaLoteEstados.Error);

        lote.EstadoCodigo = todosTerminados
            ? DteContingenciaLoteEstados.Procesado
            : DteContingenciaLoteEstados.Consultado;

        await _db.SaveChangesAsync(ct);

        var procesados = lote.Detalles.Count(d => d.EstadoCodigo == DteContingenciaLoteEstados.Procesado);
        _logger.LogInformation(
            "ContingenciaLoteService: lote {LoteId} consultado. {Proc}/{Total} DTE procesados.",
            lote.Id, procesados, lote.Detalles.Count);

        return Result<ConsultarLoteResultadoDto>.Ok(new ConsultarLoteResultadoDto
        {
            LoteId = lote.Id,
            EstadoCodigo = lote.EstadoCodigo,
            CodigoLote = lote.CodigoLote,
            DteProcesados = procesados,
            DtePendientes = lote.Detalles.Count - procesados,
            Mensaje = todosTerminados ? "Lote procesado completamente." : "Lote parcialmente procesado.",
        });
    }

    public async Task<Result<string>> ReintentarDocumentoAsync(
        int dteDocumentoId, int empresaId, CancellationToken ct = default)
    {
        var dte = await _db.DteDocumentos
            .FirstOrDefaultAsync(d => d.Id == dteDocumentoId && d.EmpresaId == empresaId, ct);
        if (dte is null)
            return Result<string>.Fail("Documento no encontrado.", "DTE_NOT_FOUND");

        if (dte.EstadoCodigo != DteEstadoCodigos.Contingencia)
            return Result<string>.Fail($"El documento está en estado {dte.EstadoCodigo}, no CONTINGENCIA.", "ESTADO_INVALIDO");

        // Reiniciar contadores para que el Worker lo tome en el próximo ciclo
        dte.UltimoIntentoRetransmisionAt = null;
        dte.IntentoRetransmision = 0;
        await _db.SaveChangesAsync(ct);

        return Result<string>.Ok("Documento marcado para reintento en el próximo ciclo del Worker.");
    }

    public async Task<int> ProcesarEventosSinLoteAsync(int empresaId, CancellationToken ct = default)
    {
        var lotesExistentes = await _db.DteContingenciaLotes
            .Where(l => l.EmpresaId == empresaId)
            .Select(l => l.EventoContingenciaId)
            .ToListAsync(ct);

        var eventosSinLote = await _db.DteEventos
            .Where(e => e.EmpresaId == empresaId
                     && e.TipoEventoCodigo == TipoEventoCodigos.Contingencia
                     && e.EstadoCodigo == DteEventoEstadoCodigos.Procesado
                     && !string.IsNullOrEmpty(e.SelloRecibido)
                     && !lotesExistentes.Contains(e.Id))
            .Select(e => e.Id)
            .ToListAsync(ct);

        var creados = 0;
        foreach (var eventoId in eventosSinLote)
        {
            var result = await CrearYEnviarLoteAsync(eventoId, empresaId, "Worker", ct);
            if (result.IsSuccess)
                creados++;
            else
                _logger.LogWarning(
                    "ContingenciaLoteService.ProcesarEventosSinLoteAsync: evento {Id} falló. {Err}",
                    eventoId, result.Error);
        }

        return creados;
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Privados
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Obtiene el token MH desde caché cifrado o hace refresh autenticando en Hacienda.
    /// Mismo patrón que DteDocumentosService.ObtenerTokenAsync.
    /// </summary>
    private async Task<(bool Success, string? Token, string? Mensaje)> ObtenerTokenAsync(
        Domain.Core.Dte.DteConfiguracion config, CancellationToken ct)
    {
        if (!string.IsNullOrEmpty(config.TokenMhCifrado)
            && config.TokenMhExpiraAt.HasValue
            && config.TokenMhExpiraAt.Value > DateTime.UtcNow.AddMinutes(5))
        {
            try
            {
                var raw = _protector.Unprotect(config.TokenMhCifrado);
                var clean = raw?.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) == true
                    ? raw[7..].Trim()
                    : raw;
                return (true, clean, null);
            }
            catch { /* fall-through al refresh */ }
        }

        if (string.IsNullOrEmpty(config.UsuarioMh) || string.IsNullOrEmpty(config.PasswordMhCifrado))
            return (false, null, "Faltan credenciales MH en Configuración DTE.");

        string password;
        try { password = _protector.Unprotect(config.PasswordMhCifrado); }
        catch { return (false, null, "No se pudo descifrar el password MH."); }

        var auth = await _haciendaAuth.AutenticarAsync(config.UsuarioMh, password, config.AmbienteCodigo, ct);
        if (!auth.Success || string.IsNullOrEmpty(auth.Token))
            return (false, null, $"Auth MH falló: [{auth.CodigoHttp}] {auth.Mensaje}");

        config.TokenMhCifrado = _protector.Protect(auth.Token);
        config.TokenMhExpiraAt = auth.ExpiresAt ?? DateTime.UtcNow.AddHours(8);
        await _db.SaveChangesAsync(ct);
        return (true, auth.Token, null);
    }

    private static ContingenciaLoteDto MapLoteDto(DteContingenciaLote lote) => new()
    {
        Id = lote.Id,
        EventoContingenciaId = lote.EventoContingenciaId,
        CodigoLote = lote.CodigoLote,
        SelloRecibido = lote.SelloRecibido,
        EstadoCodigo = lote.EstadoCodigo,
        AmbienteCodigo = lote.AmbienteCodigo,
        TotalDte = lote.Detalles.Count,
        DteProcesados = lote.Detalles.Count(d => d.EstadoCodigo == DteContingenciaLoteEstados.Procesado),
        EnviadoAt = lote.EnviadoAt,
        UltimaConsultaAt = lote.UltimaConsultaAt,
        Intentos = lote.Intentos,
        CreatedAt = lote.CreatedAt,
        RawEnvio = lote.RawEnvio,
        RawConsulta = lote.RawConsulta,
        Detalles = lote.Detalles.Select(d => new ContingenciaLoteDetalleDto
        {
            Id = d.Id,
            DteDocumentoId = d.DteDocumentoId,
            CodigoGeneracion = d.CodigoGeneracion,
            TipoDteCodigo = d.TipoDteCodigo,
            SelloRecibido = d.SelloRecibido,
            EstadoCodigo = d.EstadoCodigo,
            MensajeHacienda = d.MensajeHacienda,
        }).ToList(),
    };
}
