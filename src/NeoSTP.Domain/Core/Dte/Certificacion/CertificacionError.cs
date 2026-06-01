using NeoSTP.Domain.Common;

namespace NeoSTP.Domain.Core.Dte.Certificacion;

/// <summary>
/// Snapshot del error devuelto por Hacienda cuando un intento de certificación
/// falla. Permite trazar la matriz y mostrar diagnóstico en UI sin tener que
/// re-consultar Dte_DocumentoJson.
/// </summary>
public class CertificacionError : AuditableEntity
{
    public int PruebaId { get; set; }
    public CertificacionPrueba Prueba { get; set; } = null!;

    /// <summary>Código MH (ej. 095, 096, 802, FIRMA_FAILED).</summary>
    public string CodigoMh { get; set; } = null!;

    /// <summary>Mensaje legible del error.</summary>
    public string Descripcion { get; set; } = null!;

    /// <summary>Respuesta cruda de Hacienda (puede truncarse en UI).</summary>
    public string? RespuestaMhJson { get; set; }

    public DateTime OcurrioAt { get; set; } = DateTime.UtcNow;
}
