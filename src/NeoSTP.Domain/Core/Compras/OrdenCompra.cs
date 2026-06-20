using NeoSTP.Domain.Common;
using NeoSTP.Domain.Core.Empresas;
using NeoSTP.Domain.Core.Inventario;
using NeoSTP.Domain.Core.Productos;

namespace NeoSTP.Domain.Core.Compras;

/// <summary>Orden de compra previa a la factura/CxP del proveedor. Aislada por empresa.</summary>
public class OrdenCompra : AuditableEntity
{
    public int EmpresaId { get; set; }
    public Empresa Empresa { get; set; } = null!;

    public int ProveedorId { get; set; }
    public Proveedor Proveedor { get; set; } = null!;

    public string Numero { get; set; } = null!;
    public DateOnly Fecha { get; set; }
    public DateOnly? FechaEntregaEsperada { get; set; }
    public string EstadoCodigo { get; set; } = OrdenCompraEstados.Borrador;
    public string MonedaCodigo { get; set; } = "USD";

    public decimal Subtotal { get; set; }
    public decimal Iva { get; set; }
    public decimal Total { get; set; }
    public string? Observaciones { get; set; }

    /// <summary>Factura/CxP creada al recibir completamente la orden.</summary>
    public int? FacturaCompraId { get; set; }
    public FacturaCompra? FacturaCompra { get; set; }

    public ICollection<OrdenCompraLinea> Lineas { get; set; } = new List<OrdenCompraLinea>();
    public ICollection<OrdenCompraRecepcion> Recepciones { get; set; } = new List<OrdenCompraRecepcion>();
}

public class OrdenCompraLinea : AuditableEntity
{
    public int EmpresaId { get; set; }
    public Empresa Empresa { get; set; } = null!;

    public int OrdenCompraId { get; set; }
    public OrdenCompra OrdenCompra { get; set; } = null!;

    public int NumeroLinea { get; set; }
    public int ProductoId { get; set; }
    public Producto Producto { get; set; } = null!;

    public string Descripcion { get; set; } = null!;
    public string UnidadMedidaCodigo { get; set; } = "59";
    public decimal Cantidad { get; set; }
    public decimal PrecioUnitario { get; set; }
    public bool AplicaIva { get; set; }
    public decimal Subtotal { get; set; }
    public decimal Iva { get; set; }
    public decimal Total { get; set; }

    public ICollection<OrdenCompraRecepcionLinea> Recepciones { get; set; } = new List<OrdenCompraRecepcionLinea>();
}

/// <summary>Entrega fisica parcial o completa asociada a una orden emitida.</summary>
public class OrdenCompraRecepcion : AuditableEntity
{
    public int EmpresaId { get; set; }
    public Empresa Empresa { get; set; } = null!;

    public int OrdenCompraId { get; set; }
    public OrdenCompra OrdenCompra { get; set; } = null!;

    public string Numero { get; set; } = null!;
    public string IdempotencyKey { get; set; } = null!;
    public DateOnly Fecha { get; set; }
    public string? Referencia { get; set; }
    public string? Observaciones { get; set; }

    public ICollection<OrdenCompraRecepcionLinea> Lineas { get; set; } = new List<OrdenCompraRecepcionLinea>();
}

public class OrdenCompraRecepcionLinea : AuditableEntity
{
    public int EmpresaId { get; set; }
    public Empresa Empresa { get; set; } = null!;

    public int OrdenCompraRecepcionId { get; set; }
    public OrdenCompraRecepcion Recepcion { get; set; } = null!;

    public int OrdenCompraLineaId { get; set; }
    public OrdenCompraLinea OrdenCompraLinea { get; set; } = null!;

    public decimal Cantidad { get; set; }

    /// <summary>Entrada de kardex generada para bienes; null para servicios.</summary>
    public int? MovimientoInventarioId { get; set; }
    public MovimientoInventario? MovimientoInventario { get; set; }
}

public static class OrdenCompraEstados
{
    public const string Borrador = "BORRADOR";
    public const string Emitida = "EMITIDA";
    public const string Parcial = "PARCIAL";
    public const string Recibida = "RECIBIDA";
    public const string Cancelada = "CANCELADA";

    public static readonly string[] All = [Borrador, Emitida, Parcial, Recibida, Cancelada];
}
