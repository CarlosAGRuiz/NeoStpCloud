using System.ComponentModel.DataAnnotations;

namespace NeoSTP.Application.Inventario.Dtos;

public class ExistenciaDto
{
    public int ProductoId { get; set; }
    public string Codigo { get; set; } = null!;
    public string Nombre { get; set; } = null!;
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
    public DateOnly Fecha { get; set; }
    public string Tipo { get; set; } = null!;
    public decimal Cantidad { get; set; }
    public decimal CostoUnitario { get; set; }
    public string Origen { get; set; } = null!;
    public int? OrigenId { get; set; }
    public string? Referencia { get; set; }
    public string? Nota { get; set; }
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
    public DateOnly? Fecha { get; set; }
    [Range(0.0001, 9_999_999)] public decimal Cantidad { get; set; }
    /// <summary>Costo unitario para ENTRADA (si null, usa el costo actual del producto).</summary>
    public decimal? CostoUnitario { get; set; }
    public string Origen { get; set; } = "AJUSTE";
    public int? OrigenId { get; set; }
    [StringLength(80)] public string? Referencia { get; set; }
    [StringLength(250)] public string? Nota { get; set; }
}

public class AjusteStockRequest
{
    [Required] public int ProductoId { get; set; }
    [Range(0, 9_999_999)] public decimal CantidadAbsoluta { get; set; }
    public decimal? CostoUnitario { get; set; }
    [StringLength(250)] public string? Nota { get; set; }
}

public class SetStockMinimoRequest
{
    [Required] public int ProductoId { get; set; }
    [Range(0, 9_999_999)] public decimal StockMinimo { get; set; }
}
