using NeoSTP.Domain.Common;

namespace NeoSTP.Domain.Core.Dte.Eventos;

/// <summary>
/// Snapshot crudo de cada respuesta de Hacienda asociada a un evento.
/// Un evento puede tener varias respuestas si se reintenta su transmisión —
/// la última (mayor RecibidoAt) es la que define el estado actual.
/// </summary>
public class DteEventoRespuestaHacienda : AuditableEntity
{
    public int EventoId { get; set; }
    public DteEvento Evento { get; set; } = null!;

    /// <summary>JSON crudo que devolvió Hacienda (objeto con estado/codigoMsg/observaciones/sello).</summary>
    public string RespuestaCrudaJson { get; set; } = null!;

    /// <summary>"PROCESADO" / "RECHAZADO" / etc. extraído del body MH.</summary>
    public string? Estado { get; set; }

    /// <summary>Código MH (001 RECIBIDO, 002 OBSERVACIONES, 095, 096, 802, etc.).</summary>
    public string? CodigoMsg { get; set; }

    public string? DescripcionMsg { get; set; }

    /// <summary>Sello recibido (vacío si RECHAZADO).</summary>
    public string? SelloRecibido { get; set; }

    public DateTime RecibidoAt { get; set; } = DateTime.UtcNow;
}
