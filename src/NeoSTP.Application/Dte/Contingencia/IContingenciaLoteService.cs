using NeoSTP.Application.Common;
using NeoSTP.Application.Dte.Contingencia.Dtos;

namespace NeoSTP.Application.Dte.Contingencia;

/// <summary>
/// Servicio de contingencia avanzada — MOMENTO 3 del ciclo:
///
///   MOMENTO 1 — Emitir DTE con tipoTransmision=2 (diferido) → estado CONTINGENCIA
///   MOMENTO 2 — Enviar Evento de Contingencia → obtener sello del evento ← YA IMPLEMENTADO
///   MOMENTO 3 — Enviar lote de DTE y consultar sellos individuales ← ESTE SERVICIO
///
/// El Worker <c>RetransmisionContingenciaWorker</c> también lo invoca de forma
/// automática cuando detecta un evento de contingencia PROCESADO sin lote creado.
/// </summary>
public interface IContingenciaLoteService
{
    /// <summary>
    /// Devuelve el resumen de la situación de contingencia de la empresa:
    /// documentos pendientes, lotes activos, vencimientos.
    /// </summary>
    Task<ContingenciaResumenDto> ObtenerResumenAsync(int empresaId, CancellationToken ct = default);

    /// <summary>
    /// Lista los DTE en estado CONTINGENCIA de la empresa (sin lote aún).
    /// </summary>
    Task<IReadOnlyList<ContingenciaDocumentoDto>> ListarDocumentosPendientesAsync(int empresaId, CancellationToken ct = default);

    /// <summary>Lista los lotes de contingencia de la empresa.</summary>
    Task<IReadOnlyList<ContingenciaLoteListItemDto>> ListarLotesAsync(int empresaId, CancellationToken ct = default);

    /// <summary>Obtiene el detalle de un lote (con sus DTE).</summary>
    Task<ContingenciaLoteDto?> ObtenerLoteAsync(int loteId, int empresaId, CancellationToken ct = default);

    /// <summary>
    /// Crea un lote a partir de un Evento de Contingencia PROCESADO (con sello)
    /// y lo envía a Hacienda vía POST /fesv/recepcionlote.
    /// Si el evento ya tiene un lote, devuelve el existente.
    /// </summary>
    Task<Result<CrearLoteResultadoDto>> CrearYEnviarLoteAsync(int eventoContingenciaId, int empresaId, string actor, CancellationToken ct = default);

    /// <summary>
    /// Consulta el estado de un lote enviado y actualiza los sellos individuales
    /// de cada DTE vía GET /fesv/recepcion/consultadtelote/{codigoLote}.
    /// </summary>
    Task<Result<ConsultarLoteResultadoDto>> ConsultarLoteAsync(int loteId, int empresaId, CancellationToken ct = default);

    /// <summary>
    /// Reintentar el envío de un DTE individual en estado CONTINGENCIA
    /// (delega al DteRetransmisionService existente).
    /// </summary>
    Task<Result<string>> ReintentarDocumentoAsync(int dteDocumentoId, int empresaId, CancellationToken ct = default);

    /// <summary>
    /// Detecta eventos de contingencia PROCESADOS que aún no tienen lote creado
    /// y los procesa automáticamente (invocado por el Worker).
    /// Devuelve el número de lotes creados.
    /// </summary>
    Task<int> ProcesarEventosSinLoteAsync(int empresaId, CancellationToken ct = default);
}
