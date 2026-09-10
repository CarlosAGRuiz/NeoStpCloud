using Microsoft.EntityFrameworkCore;
using NeoSTP.Infrastructure.Dte;
using Microsoft.Extensions.Logging;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Common;
using NeoSTP.Application.Connect;
using NeoSTP.Domain.Core.Connect;
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
    private readonly IConnectWebhookDispatcher? _webhookDispatcher;

    public ContingenciaLoteService(
        NeoStpDbContext db,
        IHaciendaLoteClient loteClient,
        IHaciendaConsultaLoteClient consultaClient,
        IHaciendaAuthClient haciendaAuth,
        ISecretProtector protector,
        ILogger<ContingenciaLoteService> logger,
        IConnectWebhookDispatcher? webhookDispatcher = null)
    {
        _db = db;
        _loteClient = loteClient;
        _consultaClient = consultaClient;
        _haciendaAuth = haciendaAuth;
        _protector = protector;
        _logger = logger;
        _webhookDispatcher = webhookDispatcher;
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
        try { return await CrearYEnviarLoteCoreAsync(eventoContingenciaId, empresaId, actor, ct); }
        catch (DbUpdateConcurrencyException)
        {
            _db.ChangeTracker.Clear();
            return Result<CrearLoteResultadoDto>.Fail(
                "Otro proceso modificó un documento del lote. Recargue y consulte Hacienda antes de reenviar.", "DTE_CONCURRENCY_CONFLICT");
        }
    }

    private async Task<Result<CrearLoteResultadoDto>> CrearYEnviarLoteCoreAsync(
        int eventoContingenciaId, int empresaId, string actor, CancellationToken ct)
    {
        // Idempotencia: si ya existe un lote para este evento devolver el existente
        var loteExistente = await _db.DteContingenciaLotes
            .FirstOrDefaultAsync(l => l.EventoContingenciaId == eventoContingenciaId
                                   && l.EmpresaId == empresaId, ct);
        if (loteExistente is not null)
        {
            var existente = new CrearLoteResultadoDto
            {
                LoteId = loteExistente.Id,
                EstadoCodigo = loteExistente.EstadoCodigo,
                CodigoLote = loteExistente.CodigoLote,
                SelloRecibido = loteExistente.SelloRecibido,
                Mensaje = "El lote ya existe para este evento.",
            };
            return string.IsNullOrWhiteSpace(loteExistente.CodigoLote)
                ? Result<CrearLoteResultadoDto>.FailWithValue(existente,
                    "El lote ya tiene un intento sin código confirmado. Concilie con Hacienda; no se volvió a enviar.", "DTE_RESULTADO_INCIERTO")
                : loteExistente.EstadoCodigo == DteContingenciaLoteEstados.Error
                    ? Result<CrearLoteResultadoDto>.FailWithValue(existente,
                        "El lote existente contiene errores. Revise sus resultados individuales.", "LOTE_CON_ERRORES")
                    : Result<CrearLoteResultadoDto>.Ok(existente);
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

        var contexto = DteFiscalContext.Validar(evento.AmbienteCodigo, config);
        if (contexto.IsFailure) return Result<CrearLoteResultadoDto>.Fail(contexto.Error!, contexto.ErrorCode);
        if (documentos.Count != documentoIds.Count)
            return Result<CrearLoteResultadoDto>.Fail("El evento contiene documentos inexistentes o de otra empresa.", "DTE_NOT_FOUND");
        foreach (var tipo in documentos.Select(d => d.TipoDteCodigo).Distinct(StringComparer.Ordinal))
        {
            var autorizado = await DteTypeAuthorization.ValidateAsync(_db, empresaId, tipo, ct);
            if (autorizado.IsFailure) return Result<CrearLoteResultadoDto>.Fail(autorizado.Error!, autorizado.ErrorCode);
        }
        foreach (var doc in documentos)
        {
            var campaignAccess = await NeoSTP.Infrastructure.Dte.Certificacion.CertificationCampaignAccess.ValidateAsync(_db, empresaId, doc, ct);
            if (campaignAccess.IsFailure) return Result<CrearLoteResultadoDto>.Fail(campaignAccess.Error!, campaignAccess.ErrorCode);
            var contextoDoc = DteFiscalContext.Validar(doc.AmbienteCodigo, config);
            if (contextoDoc.IsFailure) return Result<CrearLoteResultadoDto>.Fail(contextoDoc.Error!, contextoDoc.ErrorCode);
            if (!DteFiscalContext.CoincideJws(doc.Json?.JsonFirmado, doc.AmbienteCodigo, doc))
                return Result<CrearLoteResultadoDto>.Fail("Todos los documentos del lote deben tener una firma correspondiente a su identidad y ambiente.", "DTE_PAYLOAD_INCOMPATIBLE");
            if (doc.EstadoCodigo is not (DteEstadoCodigos.Firmado or DteEstadoCodigos.Contingencia)
                || doc.EnviadoAt.HasValue || !string.IsNullOrWhiteSpace(doc.SelloRecibido))
                return Result<CrearLoteResultadoDto>.Fail(
                    "El lote contiene documentos enviados, terminales o sin firma lista. Concilie los intentos anteriores antes de transmitir.", "DTE_LOTE_ESTADO_INVALIDO");
        }
        if (await _db.DteContingenciaLoteDetalles.AnyAsync(d => documentoIds.Contains(d.DteDocumentoId), ct))
            return Result<CrearLoteResultadoDto>.Fail("Un documento ya pertenece a otro lote. Consulte ese lote sin reenviarlo.", "DTE_LOTE_EXISTENTE");

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

        // Una sola transacción de SaveChanges reserva TODOS los DTE y crea el lote.
        // Los tokens fiscales compiten con el envío individual y con otros lotes.
        // Nunca envolver la llamada HTTP en una estrategia de reintento de transacciones.
        var intentoAt = DateTime.UtcNow;
        foreach (var document in documentos)
        {
            var campaignBeforeClaim = await NeoSTP.Infrastructure.Dte.Certificacion.CertificationCampaignAccess.ValidateAsync(_db, empresaId, document, ct);
            if (campaignBeforeClaim.IsFailure) return Result<CrearLoteResultadoDto>.Fail(campaignBeforeClaim.Error!, campaignBeforeClaim.ErrorCode);
        }
        foreach (var doc in documentos)
        {
            doc.EstadoCodigo = DteEstadoCodigos.Enviado;
            doc.EnviadoAt = intentoAt;
            doc.UpdatedAt = intentoAt;
            doc.UpdatedBy = actor;
        }
        var lote = new DteContingenciaLote
        {
            EmpresaId = empresaId,
            EventoContingenciaId = eventoContingenciaId,
            EstadoCodigo = DteContingenciaLoteEstados.Enviado,
            EnviadoAt = intentoAt,
            Intentos = 1,
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
        var envio = await _loteClient.EnviarLoteAsync(new HaciendaLoteRequest
        {
            Ambiente = config.AmbienteCodigo == "PRODUCCION" ? "01" : "00",
            AmbienteCodigo = config.AmbienteCodigo,
            Nit = empresa.Nit!,
            SelloEvento = evento.SelloRecibido!,
            Items = items,
            Token = tokenResult.Token!,
        }, ct);

        lote.RawEnvio = envio.Raw ?? System.Text.Json.JsonSerializer.Serialize(new
        {
            estado = envio.Estado, codigoHttp = envio.CodigoHttp, codigoMsg = envio.CodigoMsg,
            descripcionMsg = envio.DescripcionMsg, resultadoIncierto = !envio.Success,
        });
        lote.UpdatedBy = actor;
        lote.UpdatedAt = DateTime.UtcNow;
        lote.CodigoLote = envio.CodigoLote;
        lote.SelloRecibido = envio.SelloRecibido;

        if (!envio.Success || string.IsNullOrWhiteSpace(envio.CodigoLote))
        {
            // Un error de transporte no demuestra que Hacienda no recibió el lote.
            // Se conserva ENVIADO, incluso sin código, y nunca se libera la reserva.
            await _db.SaveChangesAsync(ct);
            return Result<CrearLoteResultadoDto>.FailWithValue(new CrearLoteResultadoDto
            {
                LoteId = lote.Id, EstadoCodigo = lote.EstadoCodigo, CodigoLote = lote.CodigoLote,
                SelloRecibido = lote.SelloRecibido,
                Mensaje = "Resultado del lote sin confirmar. Consulte Hacienda antes de cualquier reenvío.",
            }, "Resultado del lote sin confirmar. Consulte Hacienda antes de cualquier reenvío.", "DTE_RESULTADO_INCIERTO");
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
        try { return await ConsultarLoteCoreAsync(loteId, empresaId, ct); }
        catch (DbUpdateConcurrencyException)
        {
            _db.ChangeTracker.Clear();
            return Result<ConsultarLoteResultadoDto>.Fail(
                "Otro proceso actualizó el lote o sus documentos. Recargue para conservar el resultado confirmado.", "DTE_CONCURRENCY_CONFLICT");
        }
    }

    private async Task<Result<ConsultarLoteResultadoDto>> ConsultarLoteCoreAsync(
        int loteId, int empresaId, CancellationToken ct)
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

        var contexto = DteFiscalContext.Validar(lote.AmbienteCodigo, config);
        if (contexto.IsFailure) return Result<ConsultarLoteResultadoDto>.Fail(contexto.Error!, contexto.ErrorCode);
        var ids = lote.Detalles.Select(d => d.DteDocumentoId).ToArray();
        var documentos = await _db.DteDocumentos.Include(d => d.Json)
            .Where(d => ids.Contains(d.Id) && d.EmpresaId == empresaId && d.AmbienteCodigo == lote.AmbienteCodigo).ToListAsync(ct);
        if (documentos.Count != ids.Distinct().Count())
            return Result<ConsultarLoteResultadoDto>.Fail("El lote contiene documentos de otro contexto fiscal.", "DTE_AMBIENTE_INCOMPATIBLE");
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

        lote.UltimaConsultaAt = DateTime.UtcNow > lote.UltimaConsultaAt.GetValueOrDefault()
            ? DateTime.UtcNow : lote.UltimaConsultaAt!.Value.AddTicks(1);
        lote.RawConsulta = consulta.Raw ?? System.Text.Json.JsonSerializer.Serialize(new
        {
            estado = consulta.Estado, codigoHttp = consulta.CodigoHttp, codigoMsg = consulta.CodigoMsg,
            descripcionMsg = consulta.DescripcionMsg,
        });

        if (!consulta.Success)
        {
            // Fallar una consulta no cambia el resultado fiscal ni detiene el sondeo.
            await _db.SaveChangesAsync(ct);
            return Result<ConsultarLoteResultadoDto>.Fail(
                consulta.DescripcionMsg ?? "Error consultando lote.", "LOTE_CONSULTA_FAILED");
        }

        if (consulta.Items.GroupBy(i => i.CodigoGeneracion, StringComparer.OrdinalIgnoreCase).Any(g => g.Count() > 1))
        {
            _db.ChangeTracker.Clear();
            return Result<ConsultarLoteResultadoDto>.Fail("La respuesta contiene documentos duplicados. No se aplicaron resultados ambiguos.", "DTE_LOTE_EVIDENCIA_CONFLICTIVA");
        }

        // Aceptar únicamente evidencia terminal explícita de miembros del lote.
        // Un sello aislado, un estado pendiente o un resultado omitido no son rechazo.
        var cambios = new List<DteDocumento>();
        foreach (var item in consulta.Items)
        {
            var detalle = lote.Detalles.FirstOrDefault(d => d.CodigoGeneracion == item.CodigoGeneracion);
            if (detalle is null) continue;
            var procesado = string.Equals(item.Estado, "PROCESADO", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(item.SelloRecibido);
            var rechazado = string.Equals(item.Estado, "RECHAZADO", StringComparison.OrdinalIgnoreCase)
                && string.IsNullOrWhiteSpace(item.SelloRecibido);
            if (!procesado && !rechazado) continue;
            var dte = documentos.Single(d => d.Id == detalle.DteDocumentoId);
            // Una consulta antigua nunca debe confirmar un JSON regenerado/reenviado.
            // Los lotes previos sin reserva durable requieren conciliación explícita.
            if (dte.EstadoCodigo != DteEstadoCodigos.Procesado
                && (!lote.EnviadoAt.HasValue || dte.EnviadoAt != lote.EnviadoAt || dte.GeneradoAt > lote.EnviadoAt))
            {
                _db.ChangeTracker.Clear();
                return Result<ConsultarLoteResultadoDto>.Fail(
                    "El documento ya no corresponde al intento de este lote. Conserve la evidencia y concilie el intento anterior con Hacienda.", "DTE_LOTE_INTENTO_INCOMPATIBLE");
            }
            if (dte.CodigoGeneracion != item.CodigoGeneracion || dte.EstadoCodigo == DteEstadoCodigos.Invalidado
                || (dte.EstadoCodigo == DteEstadoCodigos.Procesado
                    && (!procesado || dte.SelloRecibido != item.SelloRecibido))
                || (detalle.EstadoCodigo == DteContingenciaLoteEstados.Procesado
                    && (!procesado || detalle.SelloRecibido != item.SelloRecibido)))
            {
                _db.ChangeTracker.Clear();
                return Result<ConsultarLoteResultadoDto>.Fail(
                    "La consulta contradice un resultado fiscal confirmado. Se conservó la evidencia existente; requiere conciliación.", "DTE_LOTE_EVIDENCIA_CONFLICTIVA");
            }
            if (rechazado && detalle.EstadoCodigo == DteContingenciaLoteEstados.Error
                && dte.EstadoCodigo == DteEstadoCodigos.Rechazado) continue;
            detalle.SelloRecibido = item.SelloRecibido;
            detalle.MensajeHacienda = item.DescripcionMsg is { Length: > 500 } text ? text[..500] : item.DescripcionMsg;
            detalle.EstadoCodigo = procesado ? DteContingenciaLoteEstados.Procesado : DteContingenciaLoteEstados.Error;
            if (dte.EstadoCodigo == DteEstadoCodigos.Procesado) continue;
            dte.SelloRecibido = item.SelloRecibido;
            dte.EstadoCodigo = procesado ? DteEstadoCodigos.Procesado : DteEstadoCodigos.Rechazado;
            if (!cambios.Contains(dte)) cambios.Add(dte);
            if (procesado) dte.ProcesadoAt = lote.UltimaConsultaAt;
            dte.UpdatedAt = lote.UltimaConsultaAt;
            if (dte.Json is not null)
            {
                dte.Json.RespuestaHacienda = System.Text.Json.JsonSerializer.Serialize(new
                {
                    estado = item.Estado, selloRecibido = item.SelloRecibido,
                    codigoMsg = item.CodigoMsg, descripcionMsg = item.DescripcionMsg,
                    codigoGeneracion = item.CodigoGeneracion, codigoLote = lote.CodigoLote,
                });
                dte.Json.RespuestaAt = lote.UltimaConsultaAt;
            }
        }

        var todosTerminados = lote.Detalles.All(d =>
            d.EstadoCodigo == DteContingenciaLoteEstados.Procesado
         || d.EstadoCodigo == DteContingenciaLoteEstados.Error);

        var todosProcesados = lote.Detalles.Count > 0 && lote.Detalles.All(d => d.EstadoCodigo == DteContingenciaLoteEstados.Procesado);
        lote.EstadoCodigo = todosProcesados ? DteContingenciaLoteEstados.Procesado
            : todosTerminados ? DteContingenciaLoteEstados.Error : DteContingenciaLoteEstados.Enviado;

        await _db.SaveChangesAsync(ct);

        if (_webhookDispatcher is not null)
            foreach (var dte in cambios)
                await _webhookDispatcher.DispatchAsync(new ConnectDteEventoPayload
                {
                    Evento = dte.EstadoCodigo == DteEstadoCodigos.Procesado ? ConnectEventos.DteProcesado : ConnectEventos.DteRechazado,
                    EmpresaId = empresaId, DteId = dte.Id, CodigoGeneracion = dte.CodigoGeneracion,
                    TipoDte = dte.TipoDteCodigo, Estado = dte.EstadoCodigo, OcurrioAt = DateTime.UtcNow,
                }, ct);

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
            Mensaje = todosProcesados ? "Lote procesado completamente."
                : todosTerminados ? "Lote terminado con documentos rechazados; revise sus errores." : "Lote pendiente de resultados individuales.",
        });
    }

    public async Task<Result<string>> ReintentarDocumentoAsync(
        int dteDocumentoId, int empresaId, CancellationToken ct = default)
    {
        var dte = await _db.DteDocumentos
            .FirstOrDefaultAsync(d => d.Id == dteDocumentoId && d.EmpresaId == empresaId, ct);
        if (dte is null)
            return Result<string>.Fail("Documento no encontrado.", "DTE_NOT_FOUND");

        if (dte.EnviadoAt.HasValue || await _db.DteContingenciaLoteDetalles.AnyAsync(d => d.DteDocumentoId == dteDocumentoId, ct))
            return Result<string>.Fail("El documento tiene un intento previo. Concilie Hacienda antes de reintentar.", "DTE_RESULTADO_INCIERTO");

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
    private Task<(bool Success, string? Token, string? Mensaje)> ObtenerTokenAsync(
        Domain.Core.Dte.DteConfiguracion config, CancellationToken ct)
        => HaciendaTokenProvider.GetAsync(_db, config, _haciendaAuth, _protector, ct);

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
