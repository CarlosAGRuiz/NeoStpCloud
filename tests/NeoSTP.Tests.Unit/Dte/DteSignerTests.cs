using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NeoSTP.Infrastructure.Dte;
using Xunit;

namespace NeoSTP.Tests.Unit.Dte;

public class DteSignerTests
{
    private const string Sample = """{"identificacion":{"version":1,"tipoDte":"01"}}""";

    [Fact]
    public async Task MockSigner_ProduceJwsCompactoConTresSegmentos()
    {
        var s = new MockDteSignerService(NullLogger<MockDteSignerService>.Instance);
        var r = await s.FirmarAsync(Sample, certificadoBlob: null, certificadoPassword: null);

        r.Success.Should().BeTrue();
        r.JsonFirmado.Should().NotBeNullOrWhiteSpace();
        r.JsonFirmado!.Split('.').Should().HaveCount(3);
    }

    [Fact]
    public async Task MockSigner_PayloadEsBase64UrlDelJsonOriginal()
    {
        var s = new MockDteSignerService(NullLogger<MockDteSignerService>.Instance);
        var r = await s.FirmarAsync(Sample, null, null);
        var parts = r.JsonFirmado!.Split('.');
        var payload = Base64UrlDecode(parts[1]);
        Encoding.UTF8.GetString(payload).Should().Be(Sample);
    }

    [Fact]
    public async Task MockSigner_JsonVacio_Falla()
    {
        var s = new MockDteSignerService(NullLogger<MockDteSignerService>.Instance);
        var r = await s.FirmarAsync(string.Empty, null, null);
        r.Success.Should().BeFalse();
        r.Mensaje.Should().Be("JSON_VACIO");
    }

    [Fact]
    public async Task Pkcs12Signer_CertVacio_Falla()
    {
        var s = new Pkcs12DteSignerService(NullLogger<Pkcs12DteSignerService>.Instance);
        var r = await s.FirmarAsync(Sample, certificadoBlob: null, certificadoPassword: null);
        r.Success.Should().BeFalse();
        r.Mensaje.Should().Be("CERT_VACIO");
    }

    [Fact]
    public async Task Pkcs12Signer_RoundTrip_VerificableConLaLlavePublica()
    {
        // Construye un PFX en memoria con una llave RSA recién generada
        const string pwd = "test-pwd";
        var pfx = BuildSelfSignedPfx("CN=Neostp Test", pwd, out var cert);

        var s = new Pkcs12DteSignerService(NullLogger<Pkcs12DteSignerService>.Instance);
        var r = await s.FirmarAsync(Sample, pfx, pwd);

        r.Success.Should().BeTrue();
        var parts = r.JsonFirmado!.Split('.');
        parts.Should().HaveCount(3);

        // Verifica firma con la llave pública del cert
        var signingInput = Encoding.UTF8.GetBytes($"{parts[0]}.{parts[1]}");
        var signature = Base64UrlDecode(parts[2]);
        using var rsa = cert.GetRSAPublicKey()!;
        rsa.VerifyData(signingInput, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1)
            .Should().BeTrue();
    }

    [Fact]
    public async Task Pkcs12Signer_PasswordIncorrecto_Falla()
    {
        const string pwd = "real-pwd";
        var pfx = BuildSelfSignedPfx("CN=Neostp Test", pwd, out _);

        var s = new Pkcs12DteSignerService(NullLogger<Pkcs12DteSignerService>.Instance);
        var r = await s.FirmarAsync(Sample, pfx, "wrong-pwd");

        r.Success.Should().BeFalse();
        r.Mensaje.Should().Be("CERT_INVALIDO");
    }

    // ---- helpers ----

    private static byte[] BuildSelfSignedPfx(string subject, string password, out X509Certificate2 cert)
    {
        using var rsa = RSA.Create(2048);
        var req = new CertificateRequest(subject, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        cert = req.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));
        return cert.Export(X509ContentType.Pfx, password);
    }

    private static byte[] Base64UrlDecode(string s)
    {
        s = s.Replace('-', '+').Replace('_', '/');
        switch (s.Length % 4)
        {
            case 2: s += "=="; break;
            case 3: s += "="; break;
        }
        return Convert.FromBase64String(s);
    }
}
