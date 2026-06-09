namespace NeoSTP.Application.Inventario;

/// <summary>
/// Reglas puras de costeo por <b>promedio ponderado</b> (testeable sin BD).
/// - Entrada: nuevo promedio = (saldoCant·promedio + cant·costo) / (saldoCant + cant).
/// - Salida: el promedio no cambia; sólo baja la cantidad.
/// - Ajuste: fija la cantidad absoluta; si se indica costo, fija el promedio.
/// </summary>
public static class CostoPromedioCalculator
{
    public readonly record struct Saldo(decimal Cantidad, decimal CostoPromedio);

    private static decimal R(decimal v) => Math.Round(v, 4, MidpointRounding.AwayFromZero);

    public static Saldo Entrada(Saldo actual, decimal cantidad, decimal costoUnitario)
    {
        if (cantidad <= 0) return actual;
        var nuevaCant = actual.Cantidad + cantidad;
        if (nuevaCant <= 0) return new Saldo(0, actual.CostoPromedio);
        var valorActual = actual.Cantidad * actual.CostoPromedio;
        var valorEntrada = cantidad * costoUnitario;
        var promedio = R((valorActual + valorEntrada) / nuevaCant);
        return new Saldo(R(nuevaCant), promedio);
    }

    public static Saldo Salida(Saldo actual, decimal cantidad)
    {
        if (cantidad <= 0) return actual;
        var nuevaCant = actual.Cantidad - cantidad;
        if (nuevaCant < 0) nuevaCant = 0;
        return new Saldo(R(nuevaCant), actual.CostoPromedio);
    }

    /// <summary>Ajuste a una cantidad absoluta; costo opcional (si null, conserva el promedio).</summary>
    public static Saldo Ajuste(Saldo actual, decimal cantidadAbsoluta, decimal? costoUnitario)
    {
        var cant = cantidadAbsoluta < 0 ? 0 : cantidadAbsoluta;
        var costo = costoUnitario is decimal c && c >= 0 ? R(c) : actual.CostoPromedio;
        return new Saldo(R(cant), costo);
    }

    public static decimal ValorInventario(decimal cantidad, decimal costoPromedio)
        => Math.Round(cantidad * costoPromedio, 2, MidpointRounding.AwayFromZero);
}
