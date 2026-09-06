using System.Security.Cryptography;
using System.Xml;
using System.Xml.Linq;
using NeoSTP.Application.Dte.Abstractions;

namespace ClientCertificationRunner;

public static class CredentialPreflight
{
    // Hacienda's CertificadoMH XML contains an unencrypted PKCS#8 private key.
    // Certificate password is intentionally not an input: it belongs to the PFX contract.
    public static bool CertificatePairValid(byte[]? certificate)
    {
        if (certificate is null || certificate.Length == 0 || certificate.Length > 2 * 1024 * 1024) return false;
        byte[]? privateBytes = null;
        try
        {
            using var stream = new MemoryStream(certificate, writable: false);
            using var reader = XmlReader.Create(stream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null, MaxCharactersInDocument = 2 * 1024 * 1024 });
            var root = XDocument.Load(reader).Root;
            if (root?.Name.LocalName != "CertificadoMH") return false;
            string Key(string name) => root.Elements().Single(x => x.Name.LocalName == name)
                .Elements().Single(x => x.Name.LocalName == "encodied").Value;
            privateBytes = Convert.FromBase64String(Key("privateKey"));
            var publicBytes = Convert.FromBase64String(Key("publicKey"));
            using var signing = RSA.Create(); using var verifying = RSA.Create();
            signing.ImportPkcs8PrivateKey(privateBytes, out var privateRead);
            verifying.ImportSubjectPublicKeyInfo(publicBytes, out var publicRead);
            if (privateRead != privateBytes.Length || publicRead != publicBytes.Length
                || signing.KeySize is < 2048 or > 8192 || signing.KeySize != verifying.KeySize) return false;
            var challenge = RandomNumberGenerator.GetBytes(32); // Never a JSON DTE or fiscal signature.
            var signature = signing.SignData(challenge, HashAlgorithmName.SHA512, RSASignaturePadding.Pkcs1);
            return verifying.VerifyData(challenge, signature, HashAlgorithmName.SHA512, RSASignaturePadding.Pkcs1);
        }
        catch (Exception) { return false; } // Parser/key errors must not disclose private material.
        finally { if (privateBytes is not null) CryptographicOperations.ZeroMemory(privateBytes); }
    }

    public static bool HaciendaPasswordCanBeDecrypted(ISecretProtector protector, string? encrypted)
    {
        if (string.IsNullOrWhiteSpace(encrypted)) return false;
        try { return !string.IsNullOrWhiteSpace(protector.Unprotect(encrypted)); }
        catch (Exception) { return false; }
    }
}
