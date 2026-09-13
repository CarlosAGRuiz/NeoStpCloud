namespace NeoSTP.Application.Dte;

/// <summary>
/// Compatibilidad temporal para convertir certificados fiscales históricos que fueron
/// almacenados antes del envelope cifrado. En ambientes desplegados debe establecerse
/// explícitamente y quedar en false después de la conversión.
/// </summary>
public sealed class DteCertificateProtectionOptions
{
    public const string SectionName = "Dte:CertificateProtection";

    public bool AllowLegacyPlaintext { get; set; }
}
