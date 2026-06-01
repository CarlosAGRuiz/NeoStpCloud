using NeoSTP.Domain.Common;
using NeoSTP.Domain.Core.Empresas;

namespace NeoSTP.Domain.Core.Dte.Contingencia;

/// <summary>
/// Lote de transmisión (MOMENTO 3 del ciclo de contingencia).
///
/// Ciclo de vida:
///   PENDIENTE → ENVIADO → CONSULTADO → PROCESADO | ERROR
///
/// Se crea cuando el Evento de Contingencia queda PROCESADO (tiene sello).
/// El Worker envía el lote vía POST /fesv/recepcionlote y luego consulta
/// el estado de cada DTE con GET /fesv/recepcion/consultadtelote/{codigoLote}.
/// </summary>
public class DteContingenciaLote : AuditableEntity
{
    public int EmpresaId { get; set; }
    public Empresa Empresa { get; set; } = null!;

    /// <summary>
    /// Id del DteEvento de contingencia (MOMENTO 2) que originó este lote.
    /// El sello de ese evento es el que autoriza enviar el lote.
    /// </summary>
    public int EventoContingenciaId { get; set; }

    /// <summary>Código del lote asignado por Hacienda al recibir el lote (codigoLote).</summary>
    public string? CodigoLote { get; set; }

    /// <summary>Sello global del lote devuelto por Hacienda.</summary>
    public string? SelloRecibido { get; set; }

    /// <summary>Estado del lote (DteContingenciaLoteEstados).</summary>
    public string EstadoCodigo { get; set; } = DteContingenciaLoteEstados.Pendiente;

    /// <summary>"00" pruebas / "01" producción.</summary>
    public string AmbienteCodigo { get; set; } = "PRUEBAS";

    /// <summary>Timestamp en que se envió el lote a Hacienda.</summary>
    public DateTime? EnviadoAt { get; set; }

    /// <summary>Timestamp de la última consulta de estado.</summary>
    public DateTime? UltimaConsultaAt { get; set; }

    /// <summary>Respuesta cruda del envío del lote (para diagnóstico).</summary>
    public string? RawEnvio { get; set; }

    /// <summary>Respuesta cruda de la última consulta de estado.</summary>
    public string? RawConsulta { get; set; }

    /// <summary>Número de intentos de envío.</summary>
    public int Intentos { get; set; } = 0;

    public ICollection<DteContingenciaLoteDetalle> Detalles { get; set; } = new List<DteContingenciaLoteDetalle>();
}

/// <summary>Constantes de estado para DteContingenciaLote.</summary>
public static class DteContingenciaLoteEstados
{
    /// <summary>Lote creado, aún no enviado.</summary>
    public const string Pendiente = "PENDIENTE";
    /// <summary>Lote enviado a Hacienda, esperando sello del lote.</summary>
    public const string Enviado = "ENVIADO";
    /// <summary>Sello del lote recibido; consultando sellos individuales.</summary>
    public const string Consultado = "CONSULTADO";
    /// <summary>Todos los DTE del lote tienen sello individual.</summary>
    public const string Procesado = "PROCESADO";
    /// <summary>Error técnico (red, firma, etc.).</summary>
    public const string Error = "ERROR";
}
