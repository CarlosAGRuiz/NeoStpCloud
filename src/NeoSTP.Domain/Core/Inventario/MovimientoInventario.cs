using NeoSTP.Domain.Common;

namespace NeoSTP.Domain.Core.Inventario;

/// <summary>
/// Movimiento de inventario (kardex). Cada movimiento ajusta la existencia del producto y
/// guarda el saldo resultante de cantidad y costo promedio (snapshot) para trazabilidad.
/// </summary>
public class MovimientoInventario : AuditableEntity
{
    public int EmpresaId { get; set; }
    public int ProductoId { get; set; }

    /// <summary>Sucursal afectada (E2). Null = bodega central.</summary>
    public int? SucursalId { get; set; }

    public DateOnly Fecha { get; set; }

    /// <summary>ENTRADA | SALIDA | AJUSTE.</summary>
    public string Tipo { get; set; } = TiposMovimientoInventario.Entrada;

    public decimal Cantidad { get; set; }

    /// <summary>Costo unitario del movimiento (relevante en ENTRADA / AJUSTE).</summary>
    public decimal CostoUnitario { get; set; }

    /// <summary>INICIAL | COMPRA | RECEPCION_COMPRA | VENTA | AJUSTE | DEVOLUCION.</summary>
    public string Origen { get; set; } = OrigenesMovimientoInventario.Ajuste;
    public int? OrigenId { get; set; }

    public string? Referencia { get; set; }
    public string? Nota { get; set; }

    /// <summary>
    /// Lote afectado (productos con ControlaLote). En salidas FEFO que cruzan varios
    /// lotes queda "FEFO" y el detalle por lote va en <see cref="Nota"/>.
    /// </summary>
    public string? NumeroLote { get; set; }

    /// <summary>Saldo de cantidad después de aplicar el movimiento (snapshot).</summary>
    public decimal SaldoCantidad { get; set; }
    /// <summary>Costo promedio después de aplicar el movimiento (snapshot).</summary>
    public decimal SaldoCostoPromedio { get; set; }
}

public static class TiposMovimientoInventario
{
    public const string Entrada = "ENTRADA";
    public const string Salida = "SALIDA";
    public const string Ajuste = "AJUSTE";

    public static readonly string[] All = [Entrada, Salida, Ajuste];
}

public static class OrigenesMovimientoInventario
{
    public const string Inicial = "INICIAL";
    public const string Compra = "COMPRA";
    public const string RecepcionCompra = "RECEPCION_COMPRA";
    public const string Venta = "VENTA";
    public const string Ajuste = "AJUSTE";
    public const string Devolucion = "DEVOLUCION";
    public const string Traslado = "TRASLADO";

    public static readonly string[] All = [Inicial, Compra, RecepcionCompra, Venta, Ajuste, Devolucion, Traslado];
}
