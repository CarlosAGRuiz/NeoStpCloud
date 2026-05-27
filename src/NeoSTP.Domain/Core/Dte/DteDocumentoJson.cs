using NeoSTP.Domain.Common;

namespace NeoSTP.Domain.Core.Dte;

/// <summary>
/// Persistencia del JSON generado para el DTE y la respuesta de Hacienda (1-a-1 con DteDocumento).
/// </summary>
public class DteDocumentoJson : AuditableEntity
{
    public int DocumentoId { get; set; }
    public DteDocumento Documento { get; set; } = null!;

    /// <summary>JSON sin firmar, generado por DteGeneratorService.</summary>
    public string JsonDte { get; set; } = null!;

    /// <summary>JWT/JWS firmado (poblado en Sprint 6).</summary>
    public string? JsonFirmado { get; set; }

    /// <summary>Respuesta cruda de Hacienda al recibir el DTE (Sprint 6).</summary>
    public string? RespuestaHacienda { get; set; }

    public DateTime GeneradoAt { get; set; } = DateTime.UtcNow;
    public DateTime? FirmadoAt { get; set; }
    public DateTime? RespuestaAt { get; set; }
}
