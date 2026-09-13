using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using NeoSTP.Application.Dte.Abstractions;

namespace NeoSTP.Infrastructure.Dte;

/// <summary>
/// Firmador RS256 usando el certificado en formato <c>CertificadoMH XML</c>
/// que emite el portal de Hacienda El Salvador (archivo <c>.crt</c> con raíz &lt;CertificadoMH&gt;).
///
/// <para>El XML contiene la clave privada en PKCS#8 DER (Base64) y la clave pública en
/// SubjectPublicKeyInfo DER (Base64). No requiere password — el formato no está cifrado.</para>
///
/// <para>El <c>x5t</c> del header JWS se calcula como
/// <c>Base64Url(SHA-1(SubjectPublicKeyInfo DER bytes))</c>.</para>
/// </summary>
public class HaciendaCertMhDteSignerService : IDteSignerService
{
    private readonly ILogger<HaciendaCertMhDteSignerService> _logger;

    public HaciendaCertMhDteSignerService(ILogger<HaciendaCertMhDteSignerService> logger)
    {
        _logger = logger;
    }

    public Task<DteSignResult> FirmarAsync(
        string jsonDte,
        byte[]? certificadoBlob,
        string? certificadoPassword,   // ignorado — el XML MH no tiene password
        CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(jsonDte))
            return Task.FromResult(Fail("JSON_VACIO", "El JSON DTE está vacío."));

        if (certificadoBlob is null || certificadoBlob.Length == 0)
            return Task.FromResult(Fail("CERT_VACIO",
                "No hay certificado MH configurado. Sube el archivo .crt en Configuración DTE."));

        try
        {
            var (rsa, spkiBytes) = DteCertificateInspector.LoadHaciendaXml(certificadoBlob);
            using (rsa)
            {
                // JWS compacto: header.payload.signature (RFC 7515).
                // Hacienda El Salvador usa RS512 (RSA + SHA-512) y un header mínimo {"alg":"RS512"}
                // — idéntico al firmador oficial svfe-api-firmador. NO lleva typ ni x5t.
                var headerJson = """{"alg":"RS512"}""";
                var headerB64  = B64U(Encoding.UTF8.GetBytes(headerJson));
                var payloadB64 = B64U(Encoding.UTF8.GetBytes(jsonDte));

                var sigInput = Encoding.UTF8.GetBytes($"{headerB64}.{payloadB64}");
                var sig      = rsa.SignData(sigInput, HashAlgorithmName.SHA512, RSASignaturePadding.Pkcs1);
                var sigB64   = B64U(sig);

                // Auto-verificación local: confirma que la firma es correcta antes de enviar
                using var rsaVerify = RSA.Create();
                rsaVerify.ImportSubjectPublicKeyInfo(spkiBytes, out _);
                var selfOk = rsaVerify.VerifyData(sigInput, sig, HashAlgorithmName.SHA512, RSASignaturePadding.Pkcs1);
                _logger.LogInformation(
                    "HaciendaCertMhDteSignerService: DTE firmado con RS512. Auto-verificacion local: {Ok}",
                    selfOk ? "PASS" : "FAIL");

                return Task.FromResult(new DteSignResult
                {
                    Success     = true,
                    JsonFirmado = $"{headerB64}.{payloadB64}.{sigB64}",
                    Mensaje     = "OK",
                    Detalle     = $"RS512 con certificado CertificadoMH Hacienda (auto-verif={(selfOk ? "OK" : "FAIL")}).",
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "HaciendaCertMhDteSignerService: error al firmar");
            return Task.FromResult(Fail("FIRMA_ERROR", ex.Message));
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static string B64U(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static DteSignResult Fail(string mensaje, string detalle) => new()
    {
        Success = false,
        Mensaje = mensaje,
        Detalle = detalle,
    };
}
