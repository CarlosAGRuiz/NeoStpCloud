namespace NeoSTP.Application.Pos;

/// <summary>
/// Reglas puras de cálculo de una venta POS (testeable sin BD). Los precios unitarios se
/// asumen <b>IVA incluido</b> (precio de venta al público). De cada línea se extrae la porción
/// de IVA contenida; el subtotal es el neto sin IVA.
/// </summary>
public static class PosCalculator
{
    public readonly record struct LineaInput(decimal Cantidad, decimal PrecioUnitario, decimal Descuento, bool AplicaIva);
    public readonly record struct LineaCalculo(decimal Total, decimal IvaLinea, decimal Subtotal);
    public readonly record struct VentaTotales(decimal Subtotal, decimal IvaTotal, decimal TotalDescuento, decimal Total);

    private static decimal R(decimal v) => Math.Round(v, 2, MidpointRounding.AwayFromZero);

    public static LineaCalculo CalcularLinea(LineaInput l, decimal ivaTasa)
    {
        var bruto = l.PrecioUnitario * l.Cantidad;
        var total = R(bruto - l.Descuento);
        if (total < 0) total = 0m;
        var iva = l.AplicaIva && ivaTasa > 0 ? R(total - total / (1 + ivaTasa)) : 0m;
        return new LineaCalculo(total, iva, R(total - iva));
    }

    public static VentaTotales CalcularVenta(IEnumerable<LineaInput> lineas, decimal ivaTasa)
    {
        decimal subtotal = 0, iva = 0, descuento = 0, total = 0;
        foreach (var l in lineas)
        {
            var c = CalcularLinea(l, ivaTasa);
            subtotal += c.Subtotal; iva += c.IvaLinea; total += c.Total;
            descuento += l.Descuento > 0 ? R(l.Descuento) : 0m;
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
