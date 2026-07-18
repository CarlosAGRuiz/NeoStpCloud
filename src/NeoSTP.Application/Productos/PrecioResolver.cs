namespace NeoSTP.Application.Productos;

/// <summary>
/// Resolución pura de precio unitario por cantidad: aplica la escala de mayor
/// cantidad mínima que la cantidad alcance; sin escalas (o cantidad menor a todas)
/// rige el precio base.
/// </summary>
public static class PrecioResolver
{
    public static decimal Resolver(decimal precioBase, IEnumerable<(decimal CantidadMinima, decimal PrecioUnitario)> escalas, decimal cantidad)
    {
        var precio = precioBase;
        var mejorMinima = 0m;
        foreach (var e in escalas)
        {
            if (cantidad >= e.CantidadMinima && e.CantidadMinima > mejorMinima)
            {
                mejorMinima = e.CantidadMinima;
                precio = e.PrecioUnitario;
            }
        }
        return precio;
    }
}
