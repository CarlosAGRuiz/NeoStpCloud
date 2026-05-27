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
}
