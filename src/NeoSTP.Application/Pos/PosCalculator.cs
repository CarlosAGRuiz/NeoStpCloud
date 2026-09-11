using NeoSTP.Application.Productos;
using NeoSTP.Domain.Core.Productos;

namespace NeoSTP.Application.Pos;

/// <summary>
/// Reglas puras de cálculo de una venta POS (testeable sin BD), delegadas a la fuente
/// única de semántica de precios e IVA.
/// </summary>
public static class PosCalculator
{
    public readonly record struct LineaInput(decimal Cantidad, decimal PrecioUnitario, decimal Descuento, bool AplicaIva, string TipoPrecio = TipoPrecioCodigos.IvaIncluido);
    public readonly record struct LineaCalculo(decimal Total, decimal IvaLinea, decimal Subtotal);
    public readonly record struct VentaTotales(decimal Subtotal, decimal IvaTotal, decimal TotalDescuento, decimal Total);

    private static decimal R(decimal v) => Math.Round(v, 2, MidpointRounding.AwayFromZero);

    public static LineaCalculo CalcularLinea(LineaInput l, decimal ivaTasa)
    {
        var c = PrecioIvaCalculator.CalcularLinea(
            new(l.Cantidad, l.PrecioUnitario, l.Descuento, l.AplicaIva, l.TipoPrecio), ivaTasa);
        return new LineaCalculo(c.Total, c.Iva, c.Subtotal);
    }

    public static VentaTotales CalcularVenta(IEnumerable<LineaInput> lineas, decimal ivaTasa)
    {
        decimal subtotal = 0, iva = 0, descuento = 0, total = 0;
        foreach (var l in lineas)
        {
            var calculo = PrecioIvaCalculator.CalcularLinea(
                new(l.Cantidad, l.PrecioUnitario, l.Descuento, l.AplicaIva, l.TipoPrecio), ivaTasa);
            subtotal += calculo.Subtotal; iva += calculo.Iva; total += calculo.Total;
            descuento += calculo.DescuentoTotal;
        }
        return new VentaTotales(R(subtotal), R(iva), R(descuento), R(total));
    }

    /// <summary>Cambio a devolver dado el efectivo recibido (0 si no aplica o insuficiente registrado).</summary>
    public static decimal Cambio(decimal total, decimal? efectivoRecibido)
    {
        if (efectivoRecibido is not decimal recibido) return 0m;
        var c = R(recibido - total);
        return c > 0 ? c : 0m;
    }
}
