using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Security;
using FluentAssertions;
using NeoSTP.Infrastructure.Dte;

namespace NeoSTP.Tests.Unit.Dte;

public sealed class DteCertificateInspectorTests
{
    [Fact]
    public void ValidPkcs12_WithPrivateKey_IsAccepted()
    {
        const string password = "Synthetic#Only2026";
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=NeoSTP synthetic test", rsa, HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        using var certificate = request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(30));
        var pfx = certificate.Export(X509ContentType.Pkcs12, password);

        var accepted = DteCertificateInspector.TryInspect(
            pfx, password, out var inspection, out var error);

        accepted.Should().BeTrue(error);
        inspection!.Format.Should().Be("PKCS12");
        inspection.Fingerprint.Should().NotBeNullOrWhiteSpace();
        inspection.ExpiresAt.Should().NotBeNull();
    }

    [Fact]
    public void HaciendaXml_WithMatchingKeyPair_IsAccepted()
    {
        using var rsa = RSA.Create(2048);
        var privateKey = Convert.ToBase64String(rsa.ExportPkcs8PrivateKey());
        var publicKey = Convert.ToBase64String(rsa.ExportSubjectPublicKeyInfo());
        var xml = System.Text.Encoding.UTF8.GetBytes(
            $"<certificate><privateKey><encodied>{privateKey}</encodied></privateKey>" +
            $"<publicKey><encodied>{publicKey}</encodied></publicKey></certificate>");

        var accepted = DteCertificateInspector.TryInspect(
            xml, null, out var inspection, out var error);

        accepted.Should().BeTrue(error);
        inspection!.Format.Should().Be("HACIENDA_XML");
        inspection.Fingerprint.Should().HaveLength(40);
    }

    [Fact]
    public void XmlWithDtd_IsRejected()
    {
        var xml = System.Text.Encoding.UTF8.GetBytes(
            "<!DOCTYPE certificate [<!ENTITY xxe SYSTEM \"file:///synthetic\">]>" +
            "<certificate><privateKey><encodied>&xxe;</encodied></privateKey></certificate>");

        var accepted = DteCertificateInspector.TryInspect(
            xml, null, out var inspection, out var error);

        accepted.Should().BeFalse();
        inspection.Should().BeNull();
        error.Should().NotContain("synthetic");
    }
}
