using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using NeoSTP.Infrastructure.Diagnostics;
using Xunit;

namespace NeoSTP.Tests.Unit.Ops;

/// <summary>HB-7 - storage, secretos y retencion quedan cubiertos por readiness y documentacion.</summary>
public class Hb7StorageSecretRetentionTests
{
    private static IConfiguration Config(params KeyValuePair<string, string?>[] values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    private static string RepoPath(params string[] segments)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "NeoSTP.slnx")))
            dir = dir.Parent;

        dir.Should().NotBeNull("la prueba necesita ubicar la raiz del repositorio");
        return Path.Combine(new[] { dir!.FullName }.Concat(segments).ToArray());
    }

    [Fact]
    public async Task StorageHealthCheck_DatabaseProvider_ValidaLogsYQuedaHealthy()
    {
        var healthCheck = new StorageHealthCheck(Config(
            new KeyValuePair<string, string?>("Scan:Storage:Provider", "Database")));

        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext());

        result.Status.Should().Be(HealthStatus.Healthy);
        result.Description.Should().Contain("Storage OK (Database)");
        result.Description.Should().Contain("logs escribible");
    }

    [Fact]
    public async Task StorageHealthCheck_FileSystemProvider_ValidaRootConfigurado()
    {
        var root = Path.Combine(Path.GetTempPath(), $"neostp-health-{Guid.NewGuid():N}");
        try
        {
            var healthCheck = new StorageHealthCheck(Config(
                new KeyValuePair<string, string?>("Scan:Storage:Provider", "FileSystem"),
                new KeyValuePair<string, string?>("Scan:Storage:Root", root)));

            var result = await healthCheck.CheckHealthAsync(new HealthCheckContext());

            result.Status.Should().Be(HealthStatus.Healthy);
            result.Description.Should().Contain("Storage OK (FileSystem)");
            result.Description.Should().Contain("Scan:Storage:Root escribible");
            Directory.Exists(root).Should().BeTrue();
            Directory.GetFiles(root, ".health-*").Should().BeEmpty();
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task StorageHealthCheck_ProviderNoSoportado_FallaReadiness()
    {
        var healthCheck = new StorageHealthCheck(Config(
            new KeyValuePair<string, string?>("Scan:Storage:Provider", "BlobMagico")));

        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext());

        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.Description.Should().Contain("no soportado");
        result.Description.Should().Contain("Database o FileSystem");
    }

    [Fact]
    public void Hb7_DocumentacionOperativa_CubreStorageSecretosRetencion()
    {
        var doc = File.ReadAllText(RepoPath("docs", "Runbook-Storage-Secretos-Retencion.md"));
        var readme = File.ReadAllText(RepoPath("README.md"));
        var apiReadme = File.ReadAllText(RepoPath("src", "NeoSTP.Api", "README.md"));

        doc.Should().Contain("Scan:Storage:Provider");
        doc.Should().Contain("DataProtection");
        doc.Should().Contain("Jwt:Key");
        doc.Should().Contain("Scan:Gemini:ApiKey");
        doc.Should().Contain("Worker:LimpiezaAuditoria");
        doc.Should().Contain("10 anios");
        doc.Should().Contain("/health/ready");
        readme.Should().Contain("Runbook-Storage-Secretos-Retencion.md");
        apiReadme.Should().Contain("storage");
    }
}
