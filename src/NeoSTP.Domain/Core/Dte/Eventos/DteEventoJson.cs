using NeoSTP.Domain.Common;

namespace NeoSTP.Domain.Core.Dte.Eventos;

/// <summary>
/// JSON sin firmar y JWS firmado de un evento. 1:1 con DteEvento; se almacena
/// aparte para no inflar la cabecera y poder consultar/exportar JSON sin tocar
/// toda la fila del evento.
/// </summary>
public class DteEventoJson : AuditableEntity
{
    public int EventoId { get; set; }
    public DteEvento Evento { get; set; } = null!;

    /// <summary>Payload JSON sin firmar tal cual se construyó al crear el evento.</summary>
    public string JsonSinFirmar { get; set; } = null!;

    /// <summary>JWS RS512 firmado (header.payload.signature). Null hasta firmar.</summary>
    public string? JwsFirmado { get; set; }
}
