namespace NeoSTP.Application.Dte;

public class HaciendaOptions
{
    public const string SectionName = "Hacienda";

    /// <summary>Mock | Http</summary>
    public string Client { get; set; } = "Mock";

    /// <summary>URL base para ambiente PRUEBAS.</summary>
    public string PruebasBaseUrl { get; set; } = "https://apitest.dtes.mh.gob.sv";

    /// <summary>URL base para ambiente PRODUCCION.</summary>
    public string ProduccionBaseUrl { get; set; } = "https://api.dtes.mh.gob.sv";

    /// <summary>Timeout en segundos para llamadas HTTP a Hacienda.</summary>
    public int TimeoutSeconds { get; set; } = 30;
}
