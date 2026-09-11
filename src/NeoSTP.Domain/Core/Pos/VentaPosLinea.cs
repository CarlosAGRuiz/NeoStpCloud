using NeoSTP.Domain.Common;

namespace NeoSTP.Domain.Core.Pos;

/// <summary>Línea de una venta POS (snapshot del producto al momento de la venta).</summary>
public class VentaPosLinea : AuditableEntity
{
    public int VentaPosId { get; set; }
    public VentaPos VentaPos { get; set; } = null!;

    public int? ProductoId { get; set; }
    public string Codigo { get; set; } = null!;
    public string Descripcion { get; set; } = null!;

    public decimal Cantidad { get; set; }
    /// <summary>Precio unitario capturado.</summary>
    public decimal PrecioUnitario { get; set; }
    /// <summary>Snapshot de la semántica del precio al registrar la venta.</summary>
    public string TipoPrecio { get; set; } = NeoSTP.Domain.Core.Productos.TipoPrecioCodigos.IvaIncluido;
    public decimal Descuento { get; set; }
    public bool AplicaIva { get; set; } = true;

    /// <summary>IVA contenido o agregado, según <see cref="TipoPrecio"/>.</summary>
    public decimal IvaLinea { get; set; }
    /// <summary>Total pagable de la línea, IVA incluido.</summary>
    public decimal Total { get; set; }
}
