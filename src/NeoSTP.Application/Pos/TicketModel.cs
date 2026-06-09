namespace NeoSTP.Application.Pos;

/// <summary>Modelo para renderizar un ticket de venta (PDF térmico y vista imprimible).</summary>
public class TicketModel
{
    // Empresa / encabezado
    public string EmpresaNombre { get; set; } = null!;
    public string? EmpresaNit { get; set; }
    public string? EmpresaNrc { get; set; }
    public string? Direccion { get; set; }
    public string? Telefono { get; set; }
    public byte[]? LogoPng { get; set; }
    public string? Sucursal { get; set; }

    // Venta
    public string Numero { get; set; } = null!;
    public DateTime Fecha { get; set; }
    public string ClienteNombre { get; set; } = "Consumidor final";
    public string FormaPago { get; set; } = "EFECTIVO";
    public decimal? EfectivoRecibido { get; set; }
    public decimal? Cambio { get; set; }
    public string EstadoCodigo { get; set; } = "COMPLETADA";
    public string? Nota { get; set; }

    public decimal Subtotal { get; set; }
    public decimal IvaTotal { get; set; }
    public decimal TotalDescuento { get; set; }
    public decimal Total { get; set; }

    // Presentación
    public int AnchoMm { get; set; } = 80;
    public string MonedaSimbolo { get; set; } = "$";
    public string PieTicket { get; set; } = "¡Gracias por su compra!";

    public List<TicketLinea> Lineas { get; set; } = [];
}

public class TicketLinea
{
    public string Descripcion { get; set; } = null!;
    public decimal Cantidad { get; set; }
    public decimal PrecioUnitario { get; set; }
    public decimal Total { get; set; }
}
