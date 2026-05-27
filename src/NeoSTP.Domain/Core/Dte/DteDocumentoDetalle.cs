using NeoSTP.Domain.Common;
using NeoSTP.Domain.Core.Productos;

namespace NeoSTP.Domain.Core.Dte;

/// <summary>
/// Línea de detalle (cuerpo del documento) de un DTE.
/// </summary>
public class DteDocumentoDetalle : AuditableEntity
{
    public int DocumentoId { get; set; }
    public DteDocumento Documento { get; set; } = null!;

    public int NumeroLinea { get; set; }

    public int? ProductoId { get; set; }
    public Producto? Producto { get; set; }

    /// <summary>1 Bien, 2 Servicio, 3 Ambos, 4 Otros tributos por ítem.</summary>
    public int TipoItem { get; set; } = 1;

    public string Codigo { get; set; } = null!;
    public string Descripcion { get; set; } = null!;
    public string UnidadMedidaCodigo { get; set; } = "UNIDAD";

    public decimal Cantidad { get; set; }
    public decimal PrecioUnitario { get; set; }
    public decimal MontoDescuento { get; set; }

    public decimal VentaNoSujeta { get; set; }
    public decimal VentaExenta { get; set; }
    public decimal VentaGravada { get; set; }

    /// <summary>IVA calculado por línea (solo informativo; en factura ya está incluido en VentaGravada).</summary>
    public decimal IvaItem { get; set; }

    public bool NoGravado { get; set; }

    public string? Observaciones { get; set; }
}
