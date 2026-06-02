namespace NeoSTP.Application.Dte;

/// <summary>
/// Valores territoriales (división 2024) usados por los DTE v2/v3 (Donación, Exportación,
/// eventos) cuando no se puede derivar el municipio/distrito desde el catálogo DISTRITO_ES.
/// Sección "Dte:Territorial". Reemplaza los antiguos literales "23"/"03" hardcodeados.
/// </summary>
public sealed class TerritorialOptions
{
    public const string SectionName = "Dte:Territorial";

    /// <summary>Municipio (división 2024) por defecto del emisor para DTE v2/v3. Default San Salvador Centro "23".</summary>
    public string MunicipioDivision2024Default { get; set; } = "23";

    /// <summary>Distrito por defecto cuando el documento/empresa no lo especifica. Default Ayutuxtepeque "03".</summary>
    public string DistritoDefault { get; set; } = "03";
}
