using NeoSTP.Domain.Common;
using NeoSTP.Domain.Core.Productos;

namespace NeoSTP.Domain.Core.Inventario;

/// <summary>
/// Lote de un producto con control de vencimiento (farmacia, alimentos).
/// El saldo por lote se consume FEFO (primero lo que vence primero) en las salidas.
/// Solo aplica a productos con <see cref="Producto.ControlaLote"/>.
/// </summary>
public class LoteProducto : AuditableEntity
{
    public int EmpresaId { get; set; }
    public int ProductoId { get; set; }
    public Producto Producto { get; set; } = null!;

    /// <summary>Sucursal donde vive el saldo del lote (E2). Null = bodega central.</summary>
    public int? SucursalId { get; set; }

    public string NumeroLote { get; set; } = null!;

    /// <summary>Null para lotes sin vencimiento (se consumen al final en FEFO).</summary>
    public DateOnly? FechaVencimiento { get; set; }

    /// <summary>Saldo actual del lote (suma de entradas menos salidas).</summary>
    public decimal Cantidad { get; set; }

    public bool Vencido(DateOnly hoy) => FechaVencimiento is DateOnly v && v < hoy;
}
