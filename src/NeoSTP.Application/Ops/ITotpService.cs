namespace NeoSTP.Application.Ops;

/// <summary>
/// Generación y validación de códigos TOTP (RFC 6238) para el segundo factor.
/// Compatible con Google Authenticator / Authy (HMAC-SHA1, 6 dígitos, paso 30s).
/// </summary>
public interface ITotpService
{
    /// <summary>Genera un secreto aleatorio codificado en Base32 (sin padding).</summary>
    string GenerarSecreto(int bytes = 20);

    /// <summary>Construye el URI <c>otpauth://totp/...</c> para generar el QR de enrolamiento.</summary>
    string BuildOtpAuthUri(string secretBase32, string accountLabel, string issuer);

    /// <summary>
    /// Valida un código contra el secreto, aceptando ±<paramref name="ventana"/> pasos
    /// de 30s para tolerar desfase de reloj.
    /// </summary>
    bool Validar(string secretBase32, string code, int ventana = 1);

    /// <summary>Calcula el código TOTP para un instante dado (útil para pruebas).</summary>
    string GenerarCodigo(string secretBase32, DateTimeOffset instante);
}
