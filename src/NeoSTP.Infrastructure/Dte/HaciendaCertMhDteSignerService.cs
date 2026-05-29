using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
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
            var (rsa, spkiBytes) = CargarCertificado(certificadoBlob);
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

    /// <summary>
    /// Parsea el XML CertificadoMH y devuelve el RSA (con clave privada) y los bytes
    /// del SubjectPublicKeyInfo DER (para auto-verificación de la firma).
    /// </summary>
    private static (RSA rsa, byte[] spkiBytes) CargarCertificado(byte[] blob)
    {
        var xml = XDocument.Parse(Encoding.UTF8.GetString(blob));
        var root = xml.Root ?? throw new InvalidOperationException("XML vacío.");

        // Buscar los nodos con o sin namespace
        static string? GetText(XElement parent, string localName)
        {
            // Primero sin namespace, luego con cualquier namespace
            return parent.Element(localName)?.Value
                ?? parent.Elements().FirstOrDefault(e => e.Name.LocalName == localName)?.Value;
        }

        static XElement? GetChild(XElement parent, string localName)
            => parent.Element(localName)
            ?? parent.Elements().FirstOrDefault(e => e.Name.LocalName == localName);

        var privateKeyNode = GetChild(root, "privateKey")
            ?? throw new InvalidOperationException("Nodo <privateKey> no encontrado en CertificadoMH.");
        var publicKeyNode  = GetChild(root, "publicKey")
            ?? throw new InvalidOperationException("Nodo <publicKey> no encontrado en CertificadoMH.");

        var pkcs8B64 = GetText(privateKeyNode, "encodied")
            ?? throw new InvalidOperationException("Nodo <encodied> de privateKey no encontrado.");
        var spkiB64  = GetText(publicKeyNode, "encodied")
            ?? throw new InvalidOperationException("Nodo <encodied> de publicKey no encontrado.");

        // Base64 puede tener saltos de línea del XML
        var pkcs8Bytes = Convert.FromBase64String(LimpiarB64(pkcs8B64));
        var spkiBytes  = Convert.FromBase64String(LimpiarB64(spkiB64));

        // Cargar clave privada PKCS#8 DER
        var rsa = RSA.Create();
        rsa.ImportPkcs8PrivateKey(pkcs8Bytes, out _);

        return (rsa, spkiBytes);
    }

    private static string LimpiarB64(string s) =>
        s.Replace("\n", "").Replace("\r", "").Replace(" ", "").Replace("\t", "").Trim();

    private static string B64U(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static DteSignResult Fail(string mensaje, string detalle) => new()
    {
        Success = false,
        Mensaje = mensaje,
        Detalle = detalle,
    };
}
