namespace NeoSTP.Application.Compras;

public readonly record struct OrdenCompraLineaCalculo(decimal Subtotal, decimal Iva, decimal Total);

/// <summary>Calculo puro de ordenes de compra. IVA base El Salvador 13%.</summary>
public static class OrdenCompraCalculator
{
    public const decimal IvaRate = 0.13m;

    public static OrdenCompraLineaCalculo CalcularLinea(decimal cantidad, decimal precioUnitario, bool aplicaIva)
    {
        if (cantidad <= 0) throw new ArgumentOutOfRangeException(nameof(cantidad));
        if (precioUnitario < 0) throw new ArgumentOutOfRangeException(nameof(precioUnitario));

        var subtotal = decimal.Round(cantidad * precioUnitario, 2, MidpointRounding.AwayFromZero);
        var iva = aplicaIva
            ? decimal.Round(subtotal * IvaRate, 2, MidpointRounding.AwayFromZero)
            : 0m;
        return new(subtotal, iva, subtotal + iva);
    }

    public static OrdenCompraLineaCalculo Totalizar(IEnumerable<OrdenCompraLineaCalculo> lineas)
    {
        var items = lineas.ToList();
        return new(
            items.Sum(x => x.Subtotal),
            items.Sum(x => x.Iva),
            items.Sum(x => x.Total));
    }
}
