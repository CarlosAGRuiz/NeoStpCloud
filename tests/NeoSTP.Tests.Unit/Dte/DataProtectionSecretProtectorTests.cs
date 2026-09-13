using FluentAssertions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using NeoSTP.Infrastructure.Dte;
using Xunit;

namespace NeoSTP.Tests.Unit.Dte;

public class DataProtectionSecretProtectorTests
{
    private static DataProtectionSecretProtector BuildProtector()
    {
        var services = new ServiceCollection();
        services.AddDataProtection().SetApplicationName("NeoSTP.Tests");
        var provider = services.BuildServiceProvider();
        var dpProvider = provider.GetRequiredService<IDataProtectionProvider>();
        return new DataProtectionSecretProtector(dpProvider);
    }

    [Fact]
    public void Protect_Then_Unprotect_RoundTrips()
    {
        var p = BuildProtector();
        const string secret = "Mi.Password.Hacienda#2026";

        var cipher = p.Protect(secret);
        var back = p.Unprotect(cipher);

        cipher.Should().NotBe(secret);
        cipher.Length.Should().BeGreaterThan(secret.Length);
        back.Should().Be(secret);
    }

    [Fact]
    public void Protect_GeneratesDifferentCiphertext_ForSamePlaintext()
    {
        var p = BuildProtector();
        var c1 = p.Protect("hola");
        var c2 = p.Protect("hola");

        // DataProtection adds randomness; same input -> different ciphertext
        c1.Should().NotBe(c2);
        p.Unprotect(c1).Should().Be("hola");
        p.Unprotect(c2).Should().Be("hola");
    }

    [Fact]
    public void ProtectOrNull_HandlesNullAndEmpty()
    {
        var p = BuildProtector();
        p.ProtectOrNull(null).Should().BeNull();
        p.ProtectOrNull(string.Empty).Should().BeNull();
        p.UnprotectOrNull(null).Should().BeNull();
        p.UnprotectOrNull(string.Empty).Should().BeNull();
    }

    [Fact]
    public void Unprotect_TamperedCiphertext_Throws()
    {
        var p = BuildProtector();
        var cipher = p.Protect("seguro");
        var tampered = cipher.Substring(0, cipher.Length - 5) + "XXXXX";

        var act = () => p.Unprotect(tampered);
        act.Should().Throw<System.Security.Cryptography.CryptographicException>();
    }

    [Fact]
    public void ProtectBytes_Then_UnprotectBytes_RoundTrips_And_IsNonDeterministic()
    {
        var p = BuildProtector();
        var secret = new byte[] { 1, 2, 3, 4, 5 };

        var first = p.ProtectBytes(secret, "Empresa:1");
        var second = p.ProtectBytes(secret, "Empresa:1");

        p.IsProtectedBytes(first).Should().BeTrue();
        first.Should().NotEqual(secret);
        first.Should().NotEqual(second);
        p.UnprotectBytes(first, "Empresa:1").Should().Equal(secret);
        p.UnprotectBytes(second, "Empresa:1").Should().Equal(secret);
    }

    [Fact]
    public void UnprotectBytes_WithDifferentTenant_Throws()
    {
        var p = BuildProtector();
        var cipher = p.ProtectBytes(new byte[] { 10, 20, 30 }, "Empresa:1");

        var act = () => p.UnprotectBytes(cipher, "Empresa:2");

        act.Should().Throw<System.Security.Cryptography.CryptographicException>();
    }

    [Fact]
    public void UnprotectBytes_WhenTampered_Throws()
    {
        var p = BuildProtector();
        var cipher = p.ProtectBytes(new byte[] { 10, 20, 30 }, "Empresa:1");
        cipher[^1] ^= 0x01;

        var act = () => p.UnprotectBytes(cipher, "Empresa:1");

        act.Should().Throw<System.Security.Cryptography.CryptographicException>();
    }

    [Fact]
    public void IsProtectedBytes_RejectsPlaintext()
    {
        var p = BuildProtector();

        p.IsProtectedBytes(new byte[] { 1, 2, 3 }).Should().BeFalse();
    }

}
