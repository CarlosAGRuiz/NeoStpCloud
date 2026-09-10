using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace NeoSTP.Infrastructure.Diagnostics;

/// <summary>
/// Explicit deployment key storage. Provision directory ACLs and recoverable certificate custody separately.
/// Existing user/DPAPI keys are not migrated or rewrapped by this registration.
/// </summary>
public static class DataProtectionDeploymentConfiguration
{
    public const string ApplicationName = "NeoSTP.Cloud";
    private const string Failure = "DATA_PROTECTION_DEPLOYMENT_INVALID: configure an existing absolute key directory and a valid accessible RSA certificate in the selected store.";

    public static IServiceCollection AddNeoStpDataProtection(this IServiceCollection services,
        IConfiguration configuration, IHostEnvironment? environment = null)
        => AddCore(services, configuration, environment, ResolveCertificate);

    internal sealed record CertificateSelector(string Thumbprint, StoreName StoreName, StoreLocation StoreLocation);

    // The injected resolver is only a test seam; production always uses the selected read-only certificate store.
    internal static IServiceCollection AddCore(IServiceCollection services, IConfiguration configuration,
        IHostEnvironment? environment, Func<CertificateSelector, X509Certificate2?> resolveCertificate)
    {
        var section = configuration.GetSection("DataProtection");
        if (!section.Exists() && environment?.IsProduction() != true)
        {
            services.AddDataProtection().SetApplicationName(ApplicationName);
            return services;
        }

        X509Certificate2? certificate = null;
        try
        {
            var path = section["KeyRingPath"];
            var thumbprint = section["CertificateThumbprint"];
            var location = section["StoreLocation"];
            if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path)
                || string.IsNullOrWhiteSpace(thumbprint) || !Regex.IsMatch(thumbprint, "\\A[0-9a-fA-F]{40}\\z")
                || section["StoreName"] != "My" || location is not ("CurrentUser" or "LocalMachine"))
                throw new InvalidOperationException();

            var fullPath = Path.GetFullPath(path);
            if (string.Equals(fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                Path.GetPathRoot(fullPath)?.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException();

            var selector = new CertificateSelector(thumbprint.ToUpperInvariant(), StoreName.My,
                location == "LocalMachine" ? StoreLocation.LocalMachine : StoreLocation.CurrentUser);
            certificate = resolveCertificate(selector);
            if (certificate is null || !string.Equals(certificate.Thumbprint, selector.Thumbprint, StringComparison.OrdinalIgnoreCase)
                || !certificate.HasPrivateKey || certificate.NotBefore.ToUniversalTime() > DateTime.UtcNow
                || certificate.NotAfter.ToUniversalTime() <= DateTime.UtcNow)
                throw new InvalidOperationException();

            // HasPrivateKey alone does not prove that this process can actually use the private key.
            using (var privateKey = certificate.GetRSAPrivateKey())
            using (var publicKey = certificate.GetRSAPublicKey())
            {
                if (privateKey is null || publicKey is null || publicKey.KeySize < 2048)
                    throw new InvalidOperationException();
                var probe = RandomNumberGenerator.GetBytes(32);
                var recovered = privateKey.Decrypt(publicKey.Encrypt(probe, RSAEncryptionPadding.OaepSHA1), RSAEncryptionPadding.OaepSHA1);
                if (!CryptographicOperations.FixedTimeEquals(probe, recovered)) throw new InvalidOperationException();
            }

            // Never create a production directory with accidental inherited ACLs during startup.
            ValidateDirectory(fullPath, environment);
            var configuredCertificate = certificate;
            services.AddSingleton<CertificateLifetime>(_ => new CertificateLifetime(configuredCertificate));
            services.AddOptions<KeyManagementOptions>().Configure<CertificateLifetime>((_, _) => { });
            services.AddDataProtection().SetApplicationName(ApplicationName)
                .PersistKeysToFileSystem(new DirectoryInfo(fullPath))
                .ProtectKeysWithCertificate(configuredCertificate)
                .UnprotectKeysWithAnyCertificate(configuredCertificate);
            certificate = null; // Owned by the container once key-management options are resolved.
            return services;
        }
        catch (Exception)
        {
            certificate?.Dispose();
            // Supplied configuration, store errors and key-provider exceptions never escape this boundary.
            throw new InvalidOperationException(Failure);
        }
    }

    private static void ValidateDirectory(string fullPath, IHostEnvironment? environment)
    {
        if (!Directory.Exists(fullPath)) throw new InvalidOperationException();
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        var contentRoot = environment?.ContentRootPath;
        if (!string.IsNullOrWhiteSpace(contentRoot) && Path.IsPathFullyQualified(contentRoot))
        {
            var root = Path.GetFullPath(contentRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (string.Equals(fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), root, comparison)
                || fullPath.StartsWith(root + Path.DirectorySeparatorChar, comparison))
                throw new InvalidOperationException();
        }
        // This guards redirection at validation time; it does not replace operator-managed ACLs.
        for (var directory = new DirectoryInfo(fullPath); directory is not null; directory = directory.Parent)
            if ((directory.Attributes & FileAttributes.ReparsePoint) != 0) throw new InvalidOperationException();
    }
    private static X509Certificate2? ResolveCertificate(CertificateSelector selector)
    {
        using var store = new X509Store(selector.StoreName, selector.StoreLocation);
        store.Open(OpenFlags.ReadOnly | OpenFlags.OpenExistingOnly);
        var certificates = store.Certificates;
        try
        {
            var matching = certificates.Cast<X509Certificate2>()
                .Where(x => string.Equals(x.Thumbprint, selector.Thumbprint, StringComparison.OrdinalIgnoreCase)).ToArray();
            return matching.Length == 1 ? new X509Certificate2(matching[0]) : null;
        }
        finally
        {
            foreach (var certificate in certificates) certificate.Dispose();
        }
    }

    private sealed class CertificateLifetime(X509Certificate2 certificate) : IDisposable
    {
        public void Dispose() => certificate.Dispose();
    }
}
