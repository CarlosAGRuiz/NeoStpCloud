using NeoSTP.Domain.Common;
using NeoSTP.Domain.Core.Dte.Eventos;

namespace NeoSTP.Domain.Core.Dte.Certificacion;

/// <summary>
/// Intento de prueba de un escenario por una empresa. Asocia el escenario con el
/// DteDocumento que lo cubre (o intentó cubrir) y captura el estado del intento.
///
/// Una empresa puede tener varios intentos del mismo escenario (IntentoNumero crece).
/// El intento más reciente representa el estado actual del escenario para la empresa.
/// </summary>
public class CertificacionPrueba : AuditableEntity
{
    public int EmpresaId { get; set; }

    public int EscenarioId { get; set; }
    public CertificacionEscenario Escenario { get; set; } = null!;

    /// <summary>DTE asociado al intento. Null cuando solo se reservó la prueba o cuando el escenario es de un evento (ver <see cref="EventoId"/>).</summary>
    public int? DteDocumentoId { get; set; }
    public DteDocumento? DteDocumento { get; set; }

    /// <summary>
    /// Evento DTE asociado al intento — para matrices CAT-INVALIDACION/CONTINGENCIA/
    /// RETORNO/OPERACIONES_ESPECIALES (Sprint 15.5). Excluye mutuamente con DteDocumentoId.
    /// </summary>
    public int? EventoId { get; set; }
    public DteEvento? Evento { get; set; }

    /// <summary>PENDIENTE / EN_PROGRESO / COMPLETADO / ERROR.</summary>
    public string EstadoCodigo { get; set; } = CertificacionEstadoCodigos.Pendiente;

    /// <summary>Sello de Hacienda cuando el intento se procesó.</summary>
    public string? SelloRecibido { get; set; }

    /// <summary>1 para el primer intento; crece con cada reintento.</summary>
    public int IntentoNumero { get; set; } = 1;

    public DateTime? ProcesadoAt { get; set; }

    public string? Notas { get; set; }

    public ICollection<CertificacionError> Errores { get; set; } = new List<CertificacionError>();
}
