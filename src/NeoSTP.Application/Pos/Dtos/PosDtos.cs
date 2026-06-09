using System.ComponentModel.DataAnnotations;

namespace NeoSTP.Application.Pos.Dtos;

public class VentaPosDto
{
    public int Id { get; set; }
    public string Numero { get; set; } = null!;
    public DateTime Fecha { get; set; }
    public string ClienteNombre { get; set; } = null!;
    public string FormaPagoCodigo { get; set; } = null!;
    public decimal Total { get; set; }
    public string EstadoCodigo { get; set; } = null!;
    public string EstadoFacturacion { get; set; } = null!;
    public int? DteDocumentoId { get; set; }
    public int Items { get; set; }
}

public class VentaPosDetalleDto : VentaPosDto
{
    public int? SucursalId { get; set; }
    public int? PuntoVentaId { get; set; }
    public int? ClienteId { get; set; }
    public decimal Subtotal { get; set; }
    public decimal IvaTotal { get; set; }
    public decimal TotalDescuento { get; set; }
    public decimal? EfectivoRecibido { get; set; }
    public decimal? Cambio { get; set; }
    public string? Nota { get; set; }
    public List<VentaPosLineaDto> Lineas { get; set; } = [];
}

public class VentaPosLineaDto
{
    public int Id { get; set; }
    public int? ProductoId { get; set; }
    public string Codigo { get; set; } = null!;
    public string Descripcion { get; set; } = null!;
    public decimal Cantidad { get; set; }
    public decimal PrecioUnitario { get; set; }
    public decimal Descuento { get; set; }
    public bool AplicaIva { get; set; }
    public decimal IvaLinea { get; set; }
    public decimal Total { get; set; }
}

public class PosResumenDiaDto
{
    public DateOnly Fecha { get; set; }
    public int Ventas { get; set; }
    public decimal Total { get; set; }
    public decimal Efectivo { get; set; }
    public decimal Tarjeta { get; set; }
    public decimal Otros { get; set; }
}

public class CrearVentaRequest
{
    public int? SucursalId { get; set; }
    public int? PuntoVentaId { get; set; }
    public int? ClienteId { get; set; }
    [StringLength(160)] public string? ClienteNombre { get; set; }

    [Required] public string FormaPagoCodigo { get; set; } = "EFECTIVO";
    public decimal? EfectivoRecibido { get; set; }
    [StringLength(250)] public string? Nota { get; set; }

    [Required, MinLength(1, ErrorMessage = "Agrega al menos un producto.")]
    public List<CrearVentaLineaRequest> Lineas { get; set; } = [];
}

public class CrearVentaLineaRequest
{
    /// <summary>Si se indica, precio/descripción/IVA se toman del producto (salvo que se sobreescriban).</summary>
    public int? ProductoId { get; set; }
    public string? Codigo { get; set; }
    public string? Descripcion { get; set; }
    [Range(0.0001, 999999)] public decimal Cantidad { get; set; } = 1m;
    public decimal? PrecioUnitario { get; set; }
    public decimal Descuento { get; set; }
    public bool? AplicaIva { get; set; }
}

public class EnviarTicketRequest
{
    [Required, EmailAddress] public string Email { get; set; } = null!;
}
