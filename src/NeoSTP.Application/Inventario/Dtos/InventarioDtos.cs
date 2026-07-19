using System.ComponentModel.DataAnnotations;

namespace NeoSTP.Application.Inventario.Dtos;

public class ExistenciaDto
{
    public int ProductoId { get; set; }
    public string Codigo { get; set; } = null!;
    public string Nombre { get; set; } = null!;
    /// <summary>Sucursal del saldo; null = central (o consolidado cuando no se filtró).</summary>
    public int? SucursalId { get; set; }
    public decimal Cantidad { get; set; }
    public decimal CostoPromedio { get; set; }
    public decimal Valor { get; set; }
    public decimal StockMinimo { get; set; }
    public bool StockBajo { get; set; }
}

public class MovimientoInventarioDto
{
    public int Id { get; set; }
    public int ProductoId { get; set; }
    public int? SucursalId { get; set; }
    public DateOnly Fecha { get; set; }
    public string Tipo { get; set; } = null!;
    public decimal Cantidad { get; set; }
    public decimal CostoUnitario { get; set; }
    public string Origen { get; set; } = null!;
    public int? OrigenId { get; set; }
    public string? Referencia { get; set; }
    public string? Nota { get; set; }
    public string? NumeroLote { get; set; }
    public decimal SaldoCantidad { get; set; }
    public decimal SaldoCostoPromedio { get; set; }
}

public class InventarioResumenDto
{
    public decimal ValorTotal { get; set; }
    public int Productos { get; set; }
    public int ProductosBajoStock { get; set; }
    public int ProductosSinStock { get; set; }
}

public class RegistrarMovimientoInventarioRequest
{
    [Required] public int ProductoId { get; set; }
    /// <summary>Sucursal del movimiento (E2). Null = bodega central.</summary>
    public int? SucursalId { get; set; }
    public DateOnly? Fecha { get; set; }
    [Range(0.0001, 9_999_999)] public decimal Cantidad { get; set; }
    /// <summary>Costo unitario para ENTRADA (si null, usa el costo actual del producto).</summary>
    public decimal? CostoUnitario { get; set; }
    public string Origen { get; set; } = "AJUSTE";
    public int? OrigenId { get; set; }
    [StringLength(80)] public string? Referencia { get; set; }
    [StringLength(250)] public string? Nota { get; set; }

    /// <summary>
    /// Lote (productos con ControlaLote). ENTRADA: obligatorio, crea/acumula el lote.
    /// SALIDA: opcional — con lote consume ese lote; sin lote consume FEFO.
    /// </summary>
    [StringLength(40)] public string? NumeroLote { get; set; }
    /// <summary>Vencimiento del lote (solo ENTRADA; null = lote sin vencimiento).</summary>
    public DateOnly? FechaVencimiento { get; set; }
}

public class LoteDto
{
    public int Id { get; set; }
    public int ProductoId { get; set; }
    public int? SucursalId { get; set; }
    public string ProductoCodigo { get; set; } = null!;
    public string ProductoNombre { get; set; } = null!;
    public string NumeroLote { get; set; } = null!;
    public DateOnly? FechaVencimiento { get; set; }
    public decimal Cantidad { get; set; }
    public int? DiasParaVencer { get; set; }
    public bool Vencido { get; set; }
    public bool PorVencer { get; set; }
}

public class AjusteStockRequest
{
    [Required] public int ProductoId { get; set; }
    public int? SucursalId { get; set; }
    [Range(0, 9_999_999)] public decimal CantidadAbsoluta { get; set; }
    public decimal? CostoUnitario { get; set; }
    [StringLength(250)] public string? Nota { get; set; }
}

public class SetStockMinimoRequest
{
    [Required] public int ProductoId { get; set; }
    public int? SucursalId { get; set; }
    [Range(0, 9_999_999)] public decimal StockMinimo { get; set; }
}

/// <summary>Traslado entre sucursales (E2): salida en origen + entrada en destino, atómico.</summary>
public class TrasladoInventarioRequest
{
    [Required] public int ProductoId { get; set; }
    [Range(0.0001, 9_999_999)] public decimal Cantidad { get; set; }
    /// <summary>Null = bodega central.</summary>
    public int? SucursalOrigenId { get; set; }
    /// <summary>Null = bodega central.</summary>
    public int? SucursalDestinoId { get; set; }
    /// <summary>Lote a trasladar (productos con ControlaLote); vacío = FEFO en el origen.</summary>
    [StringLength(40)] public string? NumeroLote { get; set; }
    [StringLength(250)] public string? Nota { get; set; }
}
