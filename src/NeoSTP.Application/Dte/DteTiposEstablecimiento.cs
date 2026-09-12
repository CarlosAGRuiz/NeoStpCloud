namespace NeoSTP.Application.Dte;

/// <summary>
/// Códigos oficiales del catálogo MH CAT-009 (Tipo de establecimiento).
/// Acepta también los códigos internos históricos para normalizar configuraciones existentes.
/// </summary>
public static class DteTiposEstablecimiento
{
    private static readonly IReadOnlyDictionary<string, string> Codigos =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["01"] = "01",
            ["02"] = "02",
            ["04"] = "04",
            ["07"] = "07",
            ["20"] = "20",
            ["SUCURSAL"] = "01",
            ["AGENCIA"] = "01",
            ["SUCURSAL_AGENCIA"] = "01",
            ["CASA_MATRIZ"] = "02",
            ["MATRIZ"] = "02",
            ["BODEGA"] = "04",
            ["PATIO"] = "07",
            ["PREDIO"] = "07",
            ["PREDIO_PATIO"] = "07",
            ["OTRO"] = "20",
        };

    public static bool TryNormalize(string? value, out string? codigoMh)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            codigoMh = null;
            return true;
        }

        return Codigos.TryGetValue(value.Trim(), out codigoMh);
    }

    /// <summary>
    /// Devuelve un código CAT-009 listo para el JSON fiscal. Conserva el comportamiento
    /// histórico de usar Casa Matriz cuando la configuración todavía está vacía.
    /// </summary>
    public static string ForEmission(string? value)
    {
        if (!TryNormalize(value, out var codigoMh))
        {
            throw new InvalidOperationException($"Tipo de establecimiento '{value}' fuera del catálogo MH CAT-009.");
        }

        return codigoMh ?? "02";
    }
}
