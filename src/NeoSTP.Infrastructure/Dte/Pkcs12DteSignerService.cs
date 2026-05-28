using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Microsoft.Extensions.Logging;
using NeoSTP.Application.Dte.Abstractions;

namespace NeoSTP.Infrastructure.Dte;

/// <summary>
/// Firmador real RS256 usando un certificado PFX/PKCS#12.
///
/// Produce un JWS compacto: `base64url(header).base64url(payload).base64url(signature)`.
/// El header incluye el thumbprint SHA-1 del certificado como `x5t` para que el
/// receptor pueda identificar la llave usada. La firma es RSA con SHA-256 según
/// requiere Hacienda El Salvador.
/// </summary>
public class Pkcs12DteSignerService : IDteSignerService
{
    private readonly ILogger<Pkcs12DteSignerService> _logger;

    public Pkcs12DteSignerService(ILogger<Pkcs12DteSignerService> logger)
    {
        _logger = logger;
    }

    public Task<DteSignResult> FirmarAsync(string jsonDte, byte[]? certificadoBlob, string? certificadoPassword, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(jsonDte))
            return Task.FromResult(Fail("JSON_VACIO", "El JSON DTE está vacío."));

        if (certificadoBlob is null || certificadoBlob.Length == 0)
            return Task.FromResult(Fail("CERT_VACIO", "No hay certificado configurado. Sube el PFX en Configuración DTE."));

        X509Certificate2 cert;
        try
        {
            cert = X509CertificateLoader.LoadPkcs12(
                certificadoBlob,
                certificadoPassword,
                X509KeyStorageFlags.EphemeralKeySet | X509KeyStorageFlags.Exportable);
        }
        catch (CryptographicException ex)
        {
            _logger.LogWarning(ex, "Pkcs12DteSignerService: no se pudo abrir el PFX (¿password incorrecto?)");
            return Task.FromResult(Fail("CERT_INVALIDO", $"No se pudo abrir el certificado: {ex.Message}"));
        }

        using (cert)
        {
            using var rsa = cert.GetRSAPrivateKey();
            if (rsa is null)
                return Task.FromResult(Fail("CERT_SIN_LLAVE", "El certificado no contiene llave privada RSA."));

            try
            {
                var thumbprintB64 = MockDteSignerService.Base64UrlEncode(cert.GetCertHash());
                var headerJson = $$"""{"alg":"RS256","typ":"JWT","x5t":"{{thumbprintB64}}"}""";
                var headerB64 = MockDteSignerService.Base64UrlEncode(Encoding.UTF8.GetBytes(headerJson));
                var payloadB64 = MockDteSignerService.Base64UrlEncode(Encoding.UTF8.GetBytes(jsonDte));
                var signingInput = Encoding.UTF8.GetBytes($"{headerB64}.{payloadB64}");

                var signature = rsa.SignData(signingInput, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
                var signatureB64 = MockDteSignerService.Base64UrlEncode(signature);

                _logger.LogInformation(
                    "Pkcs12DteSignerService: DTE firmado con certificado {Subject} (huella {Thumb}).",
                    cert.Subject, cert.Thumbprint);

                return Task.FromResult(new DteSignResult
                {
                    Success = true,
                    JsonFirmado = $"{headerB64}.{payloadB64}.{signatureB64}",
                    Mensaje = "OK",
                    Detalle = $"RS256 con certificado {cert.Subject} (vence {cert.NotAfter:yyyy-MM-dd}).",
                });
            }
            catch (CryptographicException ex)
            {
                _logger.LogError(ex, "Pkcs12DteSignerService: error firmando");
                return Task.FromResult(Fail("FIRMA_ERROR", ex.Message));
            }
        }
    }

    private static DteSignResult Fail(string mensaje, string detalle) => new()
    {
        Success = false,
        Mensaje = mensaje,
        Detalle = detalle,
    };
}
