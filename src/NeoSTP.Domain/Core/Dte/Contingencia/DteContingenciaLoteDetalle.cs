using NeoSTP.Domain.Common;

namespace NeoSTP.Domain.Core.Dte.Contingencia;

/// <summary>
/// Detalle de un DTE incluido en un lote de contingencia (MOMENTO 3).
/// Cada fila representa un DTE informado en el evento de contingencia
/// y registra el sello individual que Hacienda asigna tras consultar el lote.
/// </summary>
public class DteContingenciaLoteDetalle : AuditableEntity
{
    public int LoteId { get; set; }
    public DteContingenciaLote Lote { get; set; } = null!;

    /// <summary>Id del DteDocumento original emitido en contingencia.</summary>
    public int DteDocumentoId { get; set; }

    /// <summary>UUID del DTE (codigoGeneracion); se incluye en el cuerpo del lote.</summary>
    public string CodigoGeneracion { get; set; } = null!;

    /// <summary>Tipo del DTE (CAT-002: 01, 03, etc.).</summary>
    public string TipoDteCodigo { get; set; } = null!;

    /// <summary>Sello individual asignado por Hacienda al consultar el lote. Null hasta PROCESADO.</summary>
    public string? SelloRecibido { get; set; }

    /// <summary>Estado individual del DTE en el lote (DteContingenciaLoteEstados).</summary>
    public string EstadoCodigo { get; set; } = DteContingenciaLoteEstados.Pendiente;

    /// <summary>Mensaje o código de error de Hacienda para este DTE (si fue rechazado).</summary>
    public string? MensajeHacienda { get; set; }
}
