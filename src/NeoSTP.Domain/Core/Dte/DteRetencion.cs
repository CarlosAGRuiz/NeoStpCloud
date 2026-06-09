using System.Text.RegularExpressions;

namespace NeoSTP.Domain.Core.Dte;

/// <summary>
/// Reglas del Comprobante de Retención (07). El esquema fe-cr-v1 de Hacienda exige que
/// <c>numDocumento</c> del cuerpo sea el <b>código de generación</b> (UUID en mayúsculas)
/// cuando el documento relacionado es electrónico (tipoGeneracion=2), o un número
/// alfanumérico de hasta 20 caracteres cuando es físico (tipoGeneracion=1).
/// </summary>
public static partial class DteRetencion
{
    /// <summary>Retención IVA 1% (agentes de retención a otros contribuyentes).</summary>
    public const string CodigoIva1 = "22";
    /// <summary>Retención IVA 13%.</summary>
    public const string CodigoIva13 = "C4";
    /// <summary>Otras retenciones IVA — casos especiales (se calcula al 13%).</summary>
    public const string CodigoOtras = "C9";

    public static readonly string[] CodigosMH = [CodigoIva1, CodigoIva13, CodigoOtras];

    [GeneratedRegex("^[A-F0-9]{8}-[A-F0-9]{4}-[A-F0-9]{4}-[A-F0-9]{4}-[A-F0-9]{12}$", RegexOptions.IgnoreCase)]
    private static partial Regex UuidRegex();

    [GeneratedRegex("^[a-zA-Z0-9]{1,20}$")]
    private static partial Regex FisicoRegex();

    /// <summary>True si el número es un código de generación (UUID) → documento electrónico.</summary>
    public static bool EsCodigoGeneracion(string? numero)
        => !string.IsNullOrWhiteSpace(numero) && UuidRegex().IsMatch(numero.Trim());

    /// <summary>True si el número es válido como documento físico (alfanumérico, máx. 20).</summary>
    public static bool EsNumeroFisicoValido(string? numero)
        => !string.IsNullOrWhiteSpace(numero) && FisicoRegex().IsMatch(numero.Trim());

    /// <summary>Tasa de retención según código MH (22 → 1%, C4/C9 → 13%).</summary>
    public static decimal Tasa(string? codigoMH) => codigoMH switch
    {
        CodigoIva13 or CodigoOtras => 0.13m,
        _ => 0.01m, // 22 (default)
    };
}
