namespace NeoSTP.Infrastructure.Scan;

/// <summary>
/// Configuración del proveedor de extracción OCR/IA Gemini (Google Generative Language API).
/// Se enlaza desde la sección <c>Scan:Gemini</c>. La <see cref="ApiKey"/> es un secreto:
/// debe venir de variables de entorno / appsettings.Local (gitignored), nunca del repo.
/// </summary>
public sealed class GeminiScanOptions
{
    /// <summary>API key de Google AI Studio (Generative Language API). Secreto.</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Modelo multimodal a usar. Por defecto Gemini Flash (rápido y económico).</summary>
    public string Model { get; set; } = "gemini-2.0-flash";

    /// <summary>Base URL del servicio. Configurable para apuntar a un proxy/emulador en tests.</summary>
    public string BaseUrl { get; set; } = "https://generativelanguage.googleapis.com";
}
