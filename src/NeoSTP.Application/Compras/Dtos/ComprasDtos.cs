using System.ComponentModel.DataAnnotations;

namespace NeoSTP.Application.Compras.Dtos;

// ── Proveedores ──────────────────────────────────────────────────────────────

public class ProveedorDto
{
    public int Id { get; set; }
    public string Codigo { get; set; } = null!;
    public string Nombre { get; set; } = null!;
    public string? Nit { get; set; }
    public string? Nrc { get; set; }
    public string? Contacto { get; set; }
    public string? Telefono { get; set; }
    public string? Email { get; set; }
    public int PlazoDiasDefault { get; set; }
    public string EstadoCodigo { get; set; } = null!;
}

public class ProveedorDetalleDto : ProveedorDto
{
    public string? Direccion { get; set; }
    public decimal SaldoPorPagar { get; set; }
    public int FacturasPendientes { get; set; }
}

public class CreateProveedorRequest
{
    [Required, StringLength(30)] public string Codigo { get; set; } = null!;
    [Required, StringLength(160)] public string Nombre { get; set; } = null!;
    [StringLength(30)] public string? Nit { get; set; }
    [StringLength(30)] public string? Nrc { get; set; }
    [StringLength(120)] public string? Contacto { get; set; }
    [StringLength(30)] public string? Telefono { get; set; }
    [StringLength(160)] public string? Email { get; set; }
    [StringLength(250)] public string? Direccion { get; set; }
    [Range(0, 365)] public int PlazoDiasDefault { get; set; }
}

public class UpdateProveedorRequest
{
    [Required, StringLength(160)] public string Nombre { get; set; } = null!;
    [StringLength(30)] public string? Nit { get; set; }
    [StringLength(30)] public string? Nrc { get; set; }
    [StringLength(120)] public string? Contacto { get; set; }
    [StringLength(30)] public string? Telefono { get; set; }
    [StringLength(160)] public string? Email { get; set; }
    [StringLength(250)] public string? Direccion { get; set; }
    [Range(0, 365)] public int PlazoDiasDefault { get; set; }
}

// ── Facturas de compra (CxP) ─────────────────────────────────────────────────

public class FacturaCompraDto
{
    public int Id { get; set; }
    public int ProveedorId { get; set; }
    public string ProveedorNombre { get; set; } = null!;
    public string NumeroDocumento { get; set; } = null!;
    public string TipoDocumento { get; set; } = null!;
    public DateOnly FechaEmision { get; set; }
    public DateOnly FechaVencimiento { get; set; }
    public string CondicionPago { get; set; } = null!;
    public decimal Total { get; set; }
    public decimal Pagado { get; set; }
    public decimal Saldo { get; set; }
    public string EstadoCodigo { get; set; } = null!;
    public bool Vencida { get; set; }
    public int DiasVencido { get; set; }
}

public class FacturaCompraDetalleDto : FacturaCompraDto
{
    public decimal Subtotal { get; set; }
    public decimal Iva { get; set; }
    public bool IvaDeducible { get; set; }
    public string? Descripcion { get; set; }
    public int? ProfitGastoId { get; set; }
    public List<PagoProveedorDto> Pagos { get; set; } = [];
}

public class PagoProveedorDto
{
    public int Id { get; set; }
    public int FacturaCompraId { get; set; }
    public DateOnly Fecha { get; set; }
    public decimal Monto { get; set; }
    public string FormaPagoCodigo { get; set; } = null!;
    public string? Referencia { get; set; }
    public string? Nota { get; set; }
    public int? MovimientoTesoreriaId { get; set; }
    public string EstadoCodigo { get; set; } = null!;
}

public class ComprasResumenDto
{
    public decimal TotalPorPagar { get; set; }
    public decimal TotalVencido { get; set; }
    public int FacturasPendientes { get; set; }
    public int Proveedores { get; set; }
}

public class CrearFacturaCompraRequest
{
    [Required] public int ProveedorId { get; set; }
    [Required, StringLength(50)] public string NumeroDocumento { get; set; } = null!;
    [Required] public string TipoDocumento { get; set; } = "FACTURA";
    public DateOnly? FechaEmision { get; set; }
    /// <summary>Si no se envía, se calcula con el plazo del proveedor.</summary>
    public DateOnly? FechaVencimiento { get; set; }
    public string CondicionPago { get; set; } = "CREDITO";
    [Range(0, 99_999_999)] public decimal Subtotal { get; set; }
    [Range(0, 99_999_999)] public decimal Iva { get; set; }
    public bool IvaDeducible { get; set; }
    [StringLength(250)] public string? Descripcion { get; set; }
    /// <summary>Si es true, registra un gasto en NeoProfit para el P&amp;L.</summary>
    public bool RegistrarGastoProfit { get; set; } = true;
}

public class RegistrarPagoProveedorRequest
{
    [Required] public int FacturaCompraId { get; set; }
    public DateOnly? Fecha { get; set; }
    [Range(0.01, 99_999_999)] public decimal Monto { get; set; }
    [Required] public string FormaPagoCodigo { get; set; } = "TRANSFERENCIA";
    [StringLength(80)] public string? Referencia { get; set; }
    [StringLength(250)] public string? Nota { get; set; }
    /// <summary>Si se indica, además genera un egreso en esa cuenta de tesorería.</summary>
    public int? CuentaTesoreriaId { get; set; }
}
