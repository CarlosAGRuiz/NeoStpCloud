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
    /// <summary>Precio unitario (IVA incluido para ítems gravados, como precio de venta al público).</summary>
    public decimal PrecioUnitario { get; set; }
    public decimal Descuento { get; set; }
    public bool AplicaIva { get; set; } = true;

    /// <summary>Porción de IVA contenida en el total de la línea.</summary>
    public decimal IvaLinea { get; set; }
    /// <summary>Total de la línea (precio × cantidad − descuento), IVA incluido.</summary>
    public decimal Total { get; set; }
}
