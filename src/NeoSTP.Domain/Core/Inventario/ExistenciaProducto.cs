using NeoSTP.Domain.Common;
using NeoSTP.Domain.Core.Empresas;
using NeoSTP.Domain.Core.Productos;

namespace NeoSTP.Domain.Core.Inventario;

/// <summary>
/// Existencia (saldo) de un producto en la empresa: cantidad disponible y costo promedio
/// ponderado. Se actualiza con cada <see cref="MovimientoInventario"/>. Aislada por EmpresaId.
/// </summary>
public class ExistenciaProducto : AuditableEntity
{
    public int EmpresaId { get; set; }
    public Empresa Empresa { get; set; } = null!;

    public int ProductoId { get; set; }
    public Producto Producto { get; set; } = null!;

    /// <summary>Sucursal del saldo (E2). Null = bodega central / sin sucursal.</summary>
    public int? SucursalId { get; set; }

    public decimal Cantidad { get; set; }

    /// <summary>Costo promedio ponderado unitario.</summary>
    public decimal CostoPromedio { get; set; }

    /// <summary>Punto de reorden; si la cantidad cae por debajo se considera stock bajo.</summary>
    public decimal StockMinimo { get; set; }
}
