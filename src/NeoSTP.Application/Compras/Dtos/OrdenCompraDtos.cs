using System.ComponentModel.DataAnnotations;

namespace NeoSTP.Application.Compras.Dtos;

public class OrdenCompraDto
{
    public int Id { get; set; }
    public string Numero { get; set; } = null!;
    public int ProveedorId { get; set; }
    public string ProveedorNombre { get; set; } = null!;
    public DateOnly Fecha { get; set; }
    public DateOnly? FechaEntregaEsperada { get; set; }
    public string EstadoCodigo { get; set; } = null!;
    public string MonedaCodigo { get; set; } = "USD";
    public decimal Subtotal { get; set; }
    public decimal Iva { get; set; }
    public decimal Total { get; set; }
    public int Lineas { get; set; }
    public int? FacturaCompraId { get; set; }
}

public class OrdenCompraDetalleDto : OrdenCompraDto
{
    public string? Observaciones { get; set; }
    public List<OrdenCompraLineaDto> Detalle { get; set; } = [];
}

public class OrdenCompraLineaDto
{
    public int Id { get; set; }
    public int NumeroLinea { get; set; }
    public int ProductoId { get; set; }
    public string ProductoCodigo { get; set; } = null!;
    public string Descripcion { get; set; } = null!;
    public string UnidadMedidaCodigo { get; set; } = null!;
    public bool EsServicio { get; set; }
    public decimal Cantidad { get; set; }
    public decimal PrecioUnitario { get; set; }
    public bool AplicaIva { get; set; }
    public decimal Subtotal { get; set; }
    public decimal Iva { get; set; }
    public decimal Total { get; set; }
}

public class GuardarOrdenCompraRequest
{
    [Required] public int ProveedorId { get; set; }
    public DateOnly? Fecha { get; set; }
    public DateOnly? FechaEntregaEsperada { get; set; }
    [StringLength(1000)] public string? Observaciones { get; set; }
    [Required, MinLength(1)] public List<GuardarOrdenCompraLineaRequest> Lineas { get; set; } = [];
}

public class GuardarOrdenCompraLineaRequest
{
    [Required] public int ProductoId { get; set; }
    [Range(0.0001, 9_999_999)] public decimal Cantidad { get; set; }
    [Range(0, 99_999_999)] public decimal PrecioUnitario { get; set; }
    public bool? AplicaIva { get; set; }
}

public class ConvertirOrdenCompraRequest
{
    [Required, StringLength(50)] public string NumeroDocumento { get; set; } = null!;
    [Required] public string TipoDocumento { get; set; } = "CCF";
    public DateOnly? FechaEmision { get; set; }
    public DateOnly? FechaVencimiento { get; set; }
    public string CondicionPago { get; set; } = "CREDITO";
    public bool IvaDeducible { get; set; } = true;
    public bool RegistrarGastoProfit { get; set; } = true;
    [StringLength(250)] public string? Descripcion { get; set; }
}
