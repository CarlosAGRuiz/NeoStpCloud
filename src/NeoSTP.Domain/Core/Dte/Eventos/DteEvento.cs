using NeoSTP.Domain.Common;
using NeoSTP.Domain.Core.Empresas;

namespace NeoSTP.Domain.Core.Dte.Eventos;

/// <summary>
/// Cabecera de un evento DTE persistido (invalidación, contingencia, retorno,
/// operaciones especiales). Cada evento captura el ciclo completo:
/// JSON sin firmar → JWS firmado → respuesta Hacienda → DTE relacionados.
///
/// Multi-tenant: lleva EmpresaId y los índices respetan ese scope.
/// </summary>
public class DteEvento : AuditableEntity
{
    public int EmpresaId { get; set; }
    public Empresa Empresa { get; set; } = null!;

    /// <summary>Uno de TipoEventoCodigos (INVALIDACION/CONTINGENCIA/RETORNO/OPERACIONES_ESPECIALES).</summary>
    public string TipoEventoCodigo { get; set; } = null!;

    /// <summary>UUID v4 mayúsculas generado al persistir; coincide con codigoGeneracion del JSON.</summary>
    public string CodigoGeneracion { get; set; } = null!;

    /// <summary>Versión del esquema MH (1, 2 ó 3 según el evento y la fecha de transmisión).</summary>
    public int Version { get; set; } = 3;

    /// <summary>00 Pruebas / 01 Producción (hereda de DteConfiguracion al momento de crear el evento).</summary>
    public string AmbienteCodigo { get; set; } = "PRUEBAS";

    /// <summary>Fecha de transmisión (local El Salvador para coincidir con el JSON enviado).</summary>
    public DateTime FechaTransmision { get; set; } = DateTime.UtcNow;

    /// <summary>Estado del ciclo de vida del evento (DteEventoEstadoCodigos).</summary>
    public string EstadoCodigo { get; set; } = DteEventoEstadoCodigos.Borrador;

    /// <summary>Sello que devuelve Hacienda al procesar. Null hasta PROCESADO.</summary>
    public string? SelloRecibido { get; set; }

    /// <summary>Para invalidación: número de control del DTE invalidado (cuando solo hay 1).</summary>
    public string? NumeroControlReferencia { get; set; }

    /// <summary>Para contingencia: motivo libre (requerido si tipoContingencia=5 OTRO).</summary>
    public string? MotivoLibre { get; set; }

    /// <summary>Marca temporal del PROCESADO/RECHAZADO definitivo.</summary>
    public DateTime? FinalizadoAt { get; set; }

    public DteEventoJson? Json { get; set; }

    public ICollection<DteEventoRespuestaHacienda> Respuestas { get; set; } = new List<DteEventoRespuestaHacienda>();

    public ICollection<DteEventoDocumentoRelacionado> DocumentosRelacionados { get; set; } = new List<DteEventoDocumentoRelacionado>();
}
