namespace NeoSTP.Application.Legal;

/// <summary>
/// Configuración de documentos legales leída desde appsettings.json → "Legal".
/// Los textos pueden incluir placeholders como {CompanyName}, {ContactEmail}, {EffectiveDate}.
/// </summary>
public class LegalOptions
{
    /// <summary>Nombre de la empresa SaaS para reemplazar en los documentos.</summary>
    public string CompanyName { get; set; } = "NeoSTP";

    /// <summary>Correo de contacto legal.</summary>
    public string ContactEmail { get; set; } = "legal@neostp.com";

    /// <summary>Fecha de vigencia de los documentos (ej. "1 de enero de 2025").</summary>
    public string EffectiveDate { get; set; } = "2025-01-01";

    /// <summary>Versión de los documentos legales (usada para registrar consentimiento).</summary>
    public string Version { get; set; } = "1.0";

    /// <summary>País de la empresa SaaS.</summary>
    public string Country { get; set; } = "El Salvador";

    /// <summary>Sitio web principal.</summary>
    public string Website { get; set; } = "https://neostp.com";
}
