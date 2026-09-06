using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using FluentAssertions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using NeoSTP.Infrastructure.Diagnostics;
using NeoSTP.Infrastructure.Dte;
using NSubstitute;

namespace NeoSTP.Tests.Unit.Ops;

public sealed class DataProtectionDeploymentConfigurationTests
{
    [Fact]
    public void Independently_created_provider_recovers_from_copied_encrypted_key_ring_and_same_certificate()
    {
        using var original = new TemporaryRing();
        using var restored = new TemporaryRing();
        using var certificate = Certificate();
        string ciphertext;
        using (var first = Provider(original.Path, certificate))
        {
            var protector = new DataProtectionSecretProtector(first.GetRequiredService<IDataProtectionProvider>());
            ciphertext = protector.Protect("synthetic-recovery-probe");
        }

        var keys = Directory.GetFiles(original.Path, "*.xml");
        keys.Should().NotBeEmpty();
        foreach (var key in keys)
        {
            var xml = File.ReadAllText(key);
            xml.Should().Contain("encryptedSecret").And.Contain("EncryptedData");
            xml.Should().NotContain("<masterKey").And.NotContain("synthetic-recovery-probe");
            File.Copy(key, System.IO.Path.Combine(restored.Path, System.IO.Path.GetFileName(key)));
        }
        // Export/import only a synthetic certificate in memory; no store or real deployment keys are touched.
        using var recoveredCertificate = X509CertificateLoader.LoadPkcs12(
            certificate.Export(X509ContentType.Pkcs12, "synthetic-test-only"), "synthetic-test-only",
            X509KeyStorageFlags.EphemeralKeySet);
        using var second = Provider(restored.Path, recoveredCertificate);
        var recoveredProtector = new DataProtectionSecretProtector(second.GetRequiredService<IDataProtectionProvider>());
        recoveredProtector.Unprotect(ciphertext).Should().Be("synthetic-recovery-probe");
        second.GetRequiredService<IOptions<DataProtectionOptions>>().Value.ApplicationDiscriminator.Should().Be("NeoSTP.Cloud");
    }

    [Fact]
    public void Different_certificate_cannot_decrypt_the_original_ring()
    {
        using var ring = new TemporaryRing();
        using var correct = Certificate();
        using var incorrect = Certificate();
        string ciphertext;
        using (var first = Provider(ring.Path, correct))
            ciphertext = first.GetRequiredService<IDataProtectionProvider>().CreateProtector(DataProtectionSecretProtector.Purpose).Protect("synthetic");

        using var second = Provider(ring.Path, incorrect);
        var action = () => second.GetRequiredService<IDataProtectionProvider>()
            .CreateProtector(DataProtectionSecretProtector.Purpose).Unprotect(ciphertext);
        action.Should().Throw<CryptographicException>();
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Staging")]
    [InlineData(null)]
    public void Absent_configuration_preserves_nonproduction_framework_defaults(string? environment)
    {
        var services = new ServiceCollection();
        services.AddNeoStpDataProtection(new ConfigurationBuilder().Build(), Environment(environment));
        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<KeyManagementOptions>>().Value;
        options.XmlRepository.Should().BeNull();
        options.XmlEncryptor.Should().BeNull();
        provider.GetRequiredService<IOptions<DataProtectionOptions>>().Value.ApplicationDiscriminator.Should().Be("NeoSTP.Cloud");
    }

    [Fact]
    public void Missing_production_configuration_fails_during_registration_without_resolving_certificate()
    {
        var resolverCalls = 0;
        Action action = () => DataProtectionDeploymentConfiguration.AddCore(new ServiceCollection(),
            new ConfigurationBuilder().Build(), Environment("Production"), _ => { resolverCalls++; return null; });
        AssertSanitized(action);
        resolverCalls.Should().Be(0);
    }

    [Theory]
    [InlineData("KeyRingPath", "relative-key-ring")]
    [InlineData("KeyRingPath", "")]
    [InlineData("CertificateThumbprint", "synthetic-sensitive-setting")]
    [InlineData("CertificateThumbprint", "")]
    [InlineData("StoreName", "Root")]
    [InlineData("StoreName", "")]
    [InlineData("StoreLocation", "2")]
    [InlineData("StoreLocation", "")]
    public void Invalid_selectors_fail_before_any_certificate_or_directory_work(string key, string value)
    {
        using var ring = new TemporaryRing();
        using var certificate = Certificate();
        var settings = Settings(ring.Path, certificate);
        settings["DataProtection:" + key] = value;
        var resolverCalls = 0;
        Action action = () => DataProtectionDeploymentConfiguration.AddCore(new ServiceCollection(), Configuration(settings),
            Environment("Production"), _ => { resolverCalls++; return null; });
        AssertSanitized(action);
        resolverCalls.Should().Be(0);
        Directory.GetFileSystemEntries(ring.Path).Should().BeEmpty();
    }

    [Fact]
    public void Missing_certificate_fails_without_creating_configured_directory_or_exposing_store_error()
    {
        using var ring = new TemporaryRing();
        using var certificate = Certificate();
        var absentPath = System.IO.Path.Combine(ring.Path, "not-created");
        Action action = () => DataProtectionDeploymentConfiguration.AddCore(new ServiceCollection(),
            Configuration(Settings(absentPath, certificate)), Environment("Production"),
            _ => throw new CryptographicException("synthetic-sensitive-store-error"));
        AssertSanitized(action);
        Directory.Exists(absentPath).Should().BeFalse();
    }

    [Theory]
    [InlineData("expired")]
    [InlineData("future")]
    [InlineData("public-only")]
    [InlineData("wrong-thumbprint")]
    [InlineData("ecdsa")]
    public void Invalid_or_unusable_certificate_fails_closed(string kind)
    {
        using var ring = new TemporaryRing();
        using var valid = Certificate();
        using var candidate = kind switch
        {
            "expired" => Certificate(DateTimeOffset.UtcNow.AddDays(-3), DateTimeOffset.UtcNow.AddDays(-2)),
            "future" => Certificate(DateTimeOffset.UtcNow.AddDays(2), DateTimeOffset.UtcNow.AddDays(3)),
            "public-only" => X509CertificateLoader.LoadCertificate(valid.Export(X509ContentType.Cert)),
            "ecdsa" => EcdsaCertificate(),
            _ => Certificate()
        };
        var settings = Settings(ring.Path, kind == "wrong-thumbprint" ? valid : candidate);
        Action action = () => DataProtectionDeploymentConfiguration.AddCore(new ServiceCollection(), Configuration(settings),
            Environment("Production"), _ => new X509Certificate2(candidate));
        AssertSanitized(action);
        Directory.GetFileSystemEntries(ring.Path).Should().BeEmpty();
    }

    [Fact]
    public void Configured_development_is_validated_too_and_directory_must_be_preprovisioned()
    {
        using var ring = new TemporaryRing();
        using var certificate = Certificate();
        var absentPath = System.IO.Path.Combine(ring.Path, "operator-must-provision");
        Action action = () => DataProtectionDeploymentConfiguration.AddCore(new ServiceCollection(),
            Configuration(Settings(absentPath, certificate)), Environment("Development"), _ => new X509Certificate2(certificate));
        AssertSanitized(action);
        Directory.Exists(absentPath).Should().BeFalse();
    }

    [Fact]
    public void Explicit_store_identity_reaches_resolver_and_repository_is_the_configured_directory()
    {
        using var ring = new TemporaryRing();
        using var certificate = Certificate();
        DataProtectionDeploymentConfiguration.CertificateSelector? selected = null;
        var services = new ServiceCollection();
        DataProtectionDeploymentConfiguration.AddCore(services, Configuration(Settings(ring.Path, certificate)),
            Environment("Production"), selector => { selected = selector; return new X509Certificate2(certificate); });
        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<KeyManagementOptions>>().Value;
        options.XmlRepository.Should().BeOfType<FileSystemXmlRepository>().Which.Directory.FullName.Should().Be(ring.Path);
        options.XmlEncryptor.Should().NotBeNull();
        selected.Should().Be(new DataProtectionDeploymentConfiguration.CertificateSelector(certificate.Thumbprint, StoreName.My, StoreLocation.LocalMachine));
    }

    [Theory]
    [InlineData("")]
    [InlineData("wwwroot")]
    [InlineData("private-keys")]
    public void Key_ring_cannot_be_inside_the_published_content_root(string child)
    {
        using var publication = new TemporaryRing();
        using var certificate = Certificate();
        var ring = System.IO.Path.Combine(publication.Path, child);
        Directory.CreateDirectory(ring);
        var environment = Environment("Production")!;
        environment.ContentRootPath.Returns(publication.Path);
        Action action = () => DataProtectionDeploymentConfiguration.AddCore(new ServiceCollection(),
            Configuration(Settings(ring, certificate)), environment, _ => new X509Certificate2(certificate));
        AssertSanitized(action);
        Directory.GetFiles(ring).Should().BeEmpty();
    }
    private static ServiceProvider Provider(string path, X509Certificate2 certificate)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        DataProtectionDeploymentConfiguration.AddCore(services, Configuration(Settings(path, certificate)),
            Environment("Production"), _ => new X509Certificate2(certificate));
        return services.BuildServiceProvider();
    }

    private static void AssertSanitized(Action action)
    {
        var error = action.Should().Throw<InvalidOperationException>().Which;
        error.Message.Should().Be("DATA_PROTECTION_DEPLOYMENT_INVALID: configure an existing absolute key directory and a valid accessible RSA certificate in the selected store.");
        error.InnerException.Should().BeNull();
    }

    private static Dictionary<string, string?> Settings(string path, X509Certificate2 certificate) => new()
    {
        ["DataProtection:KeyRingPath"] = path,
        ["DataProtection:CertificateThumbprint"] = certificate.Thumbprint,
        ["DataProtection:StoreName"] = "My",
        ["DataProtection:StoreLocation"] = "LocalMachine"
    };
    private static IConfiguration Configuration(Dictionary<string, string?> values) => new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    private static IHostEnvironment? Environment(string? name)
    {
        if (name is null) return null;
        var environment = Substitute.For<IHostEnvironment>();
        environment.EnvironmentName.Returns(name);
        return environment;
    }
    private static X509Certificate2 Certificate(DateTimeOffset? from = null, DateTimeOffset? until = null)
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest("CN=NeoSTP synthetic deployment test", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var certificate = request.CreateSelfSigned(from ?? DateTimeOffset.UtcNow.AddMinutes(-5), until ?? DateTimeOffset.UtcNow.AddDays(2));
        return X509CertificateLoader.LoadPkcs12(certificate.Export(X509ContentType.Pkcs12), null, X509KeyStorageFlags.EphemeralKeySet | X509KeyStorageFlags.Exportable);
    }
    private static X509Certificate2 EcdsaCertificate()
    {
        using var key = ECDsa.Create();
        var request = new CertificateRequest("CN=NeoSTP synthetic ECDSA test", key, HashAlgorithmName.SHA256);
        return request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddDays(2));
    }
    private sealed class TemporaryRing : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "NeoSTP-synthetic-ring-" + Guid.NewGuid().ToString("N"));
        public TemporaryRing() => Directory.CreateDirectory(Path);
        public void Dispose()
        {
            if (Directory.Exists(Path)) Directory.Delete(Path, recursive: true);
        }
    }
}
