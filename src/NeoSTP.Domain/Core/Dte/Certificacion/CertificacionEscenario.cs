using NeoSTP.Domain.Common;

namespace NeoSTP.Domain.Core.Dte.Certificacion;

/// <summary>
/// Un escenario individual de la matriz (ej. FACTURA-01, CCF-15, INVALIDACION-03).
/// Se asocian DteDocumentos vía <see cref="CertificacionPrueba"/>.
/// </summary>
public class CertificacionEscenario : AuditableEntity
{
    public int MatrizId { get; set; }
    public CertificacionMatriz Matriz { get; set; } = null!;

    /// <summary>Código único dentro de la matriz: FACTURA-01, CCF-23, etc.</summary>
    public string Codigo { get; set; } = null!;

    public string Nombre { get; set; } = null!;
    public string? Descripcion { get; set; }

    /// <summary>Orden de presentación. Coincide con el número del código por defecto.</summary>
    public int Orden { get; set; }

    public bool Activo { get; set; } = true;
}
