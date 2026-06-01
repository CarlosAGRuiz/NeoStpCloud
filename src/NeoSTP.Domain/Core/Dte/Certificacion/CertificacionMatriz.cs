using NeoSTP.Domain.Common;

namespace NeoSTP.Domain.Core.Dte.Certificacion;

/// <summary>
/// Cabecera de la matriz oficial de certificación de Hacienda. Una fila por tipo DTE
/// o evento, con la cantidad de escenarios requeridos para optar a autorización.
/// </summary>
public class CertificacionMatriz : AuditableEntity
{
    /// <summary>CAT-002 (01, 03, 04, 05, 06, 07, 08, 09, 11, 14, 15) o código de evento (INVALIDACION, CONTINGENCIA, RETORNO, OPERACIONES_ESPECIALES).</summary>
    public string TipoDteCodigo { get; set; } = null!;

    /// <summary>Nombre legible (Factura, CCF, etc.).</summary>
    public string Nombre { get; set; } = null!;

    public string? Descripcion { get; set; }

    /// <summary>Cantidad de escenarios que Hacienda exige para este tipo.</summary>
    public int EscenariosRequeridos { get; set; }

    public int Orden { get; set; }

    public bool Activo { get; set; } = true;

    public ICollection<CertificacionEscenario> Escenarios { get; set; } = new List<CertificacionEscenario>();
}
