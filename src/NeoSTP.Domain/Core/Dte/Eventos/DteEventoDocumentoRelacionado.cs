using NeoSTP.Domain.Common;

namespace NeoSTP.Domain.Core.Dte.Eventos;

/// <summary>
/// Relación N:M entre un evento y los DTE involucrados. Cada fila tiene un Rol
/// que describe la semántica (anulado, lote de contingencia, origen, objeto).
/// </summary>
public class DteEventoDocumentoRelacionado : AuditableEntity
{
    public int EventoId { get; set; }
    public DteEvento Evento { get; set; } = null!;

    public int DocumentoId { get; set; }
    public DteDocumento Documento { get; set; } = null!;

    /// <summary>Uno de DteEventoRolCodigos (ANULADO / LOTE_CONTINGENCIA / ORIGEN / OBJETO).</summary>
    public string RolCodigo { get; set; } = null!;

    /// <summary>Snapshot del NumeroControl al momento del evento (por si el DTE cambia).</summary>
    public string? NumeroControlSnapshot { get; set; }
}
