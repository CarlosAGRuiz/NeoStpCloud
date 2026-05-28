namespace NeoSTP.Application.Dte.Abstractions;

public class DteSignResult
{
    public bool Success { get; set; }
    public string? JsonFirmado { get; set; }
    public string? Mensaje { get; set; }
    public string? Detalle { get; set; }
}

/// <summary>
/// Firma el JSON DTE con el certificado de la empresa (Sprint 6).
///
/// El esquema oficial de Hacienda El Salvador requiere un JWS (JSON Web Signature)
/// con algoritmo RS256 sobre el JSON del documento. La cadena resultante tiene
/// la forma `base64url(header).base64url(payload).base64url(signature)` y se envía
/// como campo `documento` al endpoint de recepción.
/// </summary>
public interface IDteSignerService
{
    /// <summary>
    /// Firma el JSON DTE usando el certificado PFX configurado para la empresa.
    /// </summary>
    /// <param name="jsonDte">JSON del documento sin firmar.</param>
    /// <param name="certificadoBlob">Bytes del archivo PFX/PKCS#12.</param>
    /// <param name="certificadoPassword">Password en claro del PFX (descifrado antes de invocar).</param>
    Task<DteSignResult> FirmarAsync(string jsonDte, byte[]? certificadoBlob, string? certificadoPassword, CancellationToken ct = default);
}
