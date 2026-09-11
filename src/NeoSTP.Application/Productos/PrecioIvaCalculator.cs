using NeoSTP.Domain.Core.Productos;

namespace NeoSTP.Application.Productos;

/// <summary>
/// Fuente única de cálculo para precios de venta con IVA incluido o excluido.
/// Los importes monetarios finales se redondean a dos decimales con criterio comercial.
/// </summary>
public static class PrecioIvaCalculator
{
    public readonly record struct LineaInput(
        decimal Cantidad,
        decimal PrecioUnitario,
        decimal Descuento,
        bool AplicaIva,
        string TipoPrecio = TipoPrecioCodigos.IvaIncluido);

    public readonly record struct LineaCalculo(
        decimal Subtotal,
        decimal Iva,
        decimal Total,
        decimal DescuentoTotal);

    public static LineaCalculo CalcularLinea(LineaInput linea, decimal ivaTasa)
    {
        var tipo = TipoPrecioCodigos.Normalizar(linea.TipoPrecio);
        if (!TipoPrecioCodigos.EsValido(tipo))
            throw new ArgumentException($"Tipo de precio inválido: {linea.TipoPrecio}.", nameof(linea));

        var bruto = Math.Max(0m, linea.Cantidad * linea.PrecioUnitario);
        var descuento = Math.Clamp(linea.Descuento, 0m, bruto);
        var importeCapturado = Math.Max(0m, bruto - descuento);

        if (!linea.AplicaIva || ivaTasa <= 0m)
        {
            var totalSinIva = Round2(importeCapturado);
            return new LineaCalculo(totalSinIva, 0m, totalSinIva, Round2(descuento));
        }

        if (tipo == TipoPrecioCodigos.IvaExcluido)
        {
            var subtotal = Round2(importeCapturado);
            var iva = Round2(subtotal * ivaTasa);
            return new LineaCalculo(
                subtotal,
                iva,
                Round2(subtotal + iva),
                Round2(descuento * (1m + ivaTasa)));
        }

        var total = Round2(importeCapturado);
        var ivaIncluido = Round2(total * ivaTasa / (1m + ivaTasa));
        return new LineaCalculo(
            Round2(total - ivaIncluido),
            ivaIncluido,
            total,
            Round2(descuento));
    }

    /// <summary>
    /// Convierte un precio o descuento entre ambas semánticas. Los ítems no gravados
    /// se conservan porque su importe no contiene IVA.
    /// </summary>
    public static decimal ConvertirImporte(
        decimal importe,
        bool aplicaIva,
        string tipoOrigen,
        string tipoDestino,
        decimal ivaTasa,
        int decimales = 4)
    {
        var origen = TipoPrecioCodigos.Normalizar(tipoOrigen);
        var destino = TipoPrecioCodigos.Normalizar(tipoDestino);
        if (!TipoPrecioCodigos.EsValido(origen) || !TipoPrecioCodigos.EsValido(destino))
            throw new ArgumentException("El tipo de precio debe ser IVA_INCLUIDO o IVA_EXCLUIDO.");
        if (!aplicaIva || ivaTasa <= 0m || origen == destino)
            return Math.Round(importe, decimales, MidpointRounding.AwayFromZero);

        var convertido = origen == TipoPrecioCodigos.IvaIncluido
            ? importe / (1m + ivaTasa)
            : importe * (1m + ivaTasa);
        return Math.Round(convertido, decimales, MidpointRounding.AwayFromZero);
    }

    private static decimal Round2(decimal value)
        => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
