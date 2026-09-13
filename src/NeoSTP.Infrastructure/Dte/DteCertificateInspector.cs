using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace NeoSTP.Infrastructure.Dte;

internal sealed record DteCertificateInspection(
    string Format,
    string Fingerprint,
    DateTime? IssuedAt,
    DateTime? ExpiresAt);

internal static class DteCertificateInspector
{
    private const long MaxXmlCharacters = 10 * 1024 * 1024;

    public static bool TryInspect(byte[] bytes, string? password,
        out DteCertificateInspection? inspection, out string? error)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        try
        {
            inspection = LooksLikeXml(bytes)
                ? InspectHaciendaXml(bytes)
                : InspectPkcs12(bytes, password);
            error = null;
            return true;
        }
        catch (Exception ex) when (ex is CryptographicException
            or XmlException or FormatException or InvalidOperationException)
        {
            inspection = null;
            error = "El archivo no contiene un certificado fiscal utilizable con una clave privada válida.";
            return false;
        }
    }

    public static (RSA PrivateKey, byte[] PublicKey) LoadHaciendaXml(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes, writable: false);
        using var reader = XmlReader.Create(stream, new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            MaxCharactersInDocument = MaxXmlCharacters,
        });
        var xml = XDocument.Load(reader, LoadOptions.None);
        var root = xml.Root ?? throw new InvalidOperationException("XML vacío.");

        static XElement? Child(XElement parent, string localName)
            => parent.Elements().FirstOrDefault(x =>
                string.Equals(x.Name.LocalName, localName, StringComparison.OrdinalIgnoreCase));

        static string Text(XElement parent, string localName)
            => Child(parent, localName)?.Value
               ?? throw new InvalidOperationException($"Nodo {localName} requerido.");

        var privateKey = Child(root, "privateKey")
            ?? throw new InvalidOperationException("Clave privada requerida.");
        var publicKey = Child(root, "publicKey")
            ?? throw new InvalidOperationException("Clave pública requerida.");

        var pkcs8 = Convert.FromBase64String(CleanBase64(Text(privateKey, "encodied")));
        var spki = Convert.FromBase64String(CleanBase64(Text(publicKey, "encodied")));

        var rsa = RSA.Create();
        try
        {
            rsa.ImportPkcs8PrivateKey(pkcs8, out var privateRead);
            if (privateRead != pkcs8.Length)
                throw new CryptographicException("Clave privada con datos adicionales.");
            using var verify = RSA.Create();
            verify.ImportSubjectPublicKeyInfo(spki, out var publicRead);
            if (publicRead != spki.Length)
                throw new CryptographicException("Clave pública con datos adicionales.");
            VerifyKeyPair(rsa, verify);
            return (rsa, spki);
        }
        catch
        {
            rsa.Dispose();
            throw;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(pkcs8);
        }
    }

    private static DteCertificateInspection InspectHaciendaXml(byte[] bytes)
    {
        var (privateKey, publicKeyBytes) = LoadHaciendaXml(bytes);
        using (privateKey)
        {
            return new DteCertificateInspection(
                "HACIENDA_XML",
                Convert.ToHexString(SHA1.HashData(publicKeyBytes)).ToLowerInvariant(),
                null,
                null);
        }
    }

    private static DteCertificateInspection InspectPkcs12(byte[] bytes, string? password)
    {
        using var certificate = X509CertificateLoader.LoadPkcs12(
            bytes,
            password,
            X509KeyStorageFlags.EphemeralKeySet);
        using var privateKey = certificate.GetRSAPrivateKey()
            ?? throw new CryptographicException("El certificado no contiene clave privada RSA.");
        using var publicKey = certificate.GetRSAPublicKey()
            ?? throw new CryptographicException("El certificado no contiene clave pública RSA.");
        VerifyKeyPair(privateKey, publicKey);
        return new DteCertificateInspection(
            "PKCS12",
            certificate.Thumbprint.ToLowerInvariant(),
            certificate.NotBefore.ToUniversalTime(),
            certificate.NotAfter.ToUniversalTime());
    }

    private static void VerifyKeyPair(RSA privateKey, RSA publicKey)
    {
        Span<byte> probe = stackalloc byte[32];
        RandomNumberGenerator.Fill(probe);
        var signature = privateKey.SignData(probe, HashAlgorithmName.SHA512, RSASignaturePadding.Pkcs1);
        try
        {
            if (!publicKey.VerifyData(probe, signature, HashAlgorithmName.SHA512, RSASignaturePadding.Pkcs1))
                throw new CryptographicException("La clave pública no corresponde a la clave privada.");
        }
        finally
        {
            CryptographicOperations.ZeroMemory(signature);
            CryptographicOperations.ZeroMemory(probe);
        }
    }

    private static bool LooksLikeXml(byte[] bytes)
    {
        var text = Encoding.UTF8.GetString(bytes.AsSpan(0, Math.Min(bytes.Length, 256)));
        return text.TrimStart('\uFEFF', ' ', '\t', '\r', '\n').StartsWith('<');
    }

    private static string CleanBase64(string value)
        => string.Concat(value.Where(c => !char.IsWhiteSpace(c)));
}
