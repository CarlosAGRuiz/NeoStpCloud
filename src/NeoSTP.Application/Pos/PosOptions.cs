namespace NeoSTP.Application.Pos;

/// <summary>Parámetros del punto de venta (parametrizables por configuración, sección "Pos").</summary>
public class PosOptions
{
    public const string SectionName = "Pos";

    /// <summary>Tasa de IVA (El Salvador 13%). Los precios de venta se asumen IVA incluido.</summary>
    public decimal IvaTasa { get; set; } = 0.13m;

    /// <summary>Ancho del ticket en mm (58 u 80).</summary>
    public int AnchoTicketMm { get; set; } = 80;

    /// <summary>Símbolo de moneda para el ticket.</summary>
    public string MonedaSimbolo { get; set; } = "$";

    /// <summary>Pie de página del ticket (mensaje de agradecimiento).</summary>
    public string PieTicket { get; set; } = "¡Gracias por su compra!";
}
