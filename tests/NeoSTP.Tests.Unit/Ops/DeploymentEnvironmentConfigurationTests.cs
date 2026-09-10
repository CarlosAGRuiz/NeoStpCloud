using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace NeoSTP.Tests.Unit.Ops;

public sealed class DeploymentEnvironmentConfigurationTests
{
    public static IEnumerable<object[]> HostEnvironments()
    {
        foreach (var host in new[] { "NeoSTP.Api", "NeoSTP.Web", "NeoSTP.Worker" })
        foreach (var environment in new[] { "Staging", "Production" })
            yield return [host, environment];
    }

    [Theory]
    [MemberData(nameof(HostEnvironments))]
    public void DeploymentOverlay_DefinesIsolatedSafeDefaults(string host, string environment)
    {
        var config = Load(host, environment);
        var expectedId = environment.ToUpperInvariant();
        var expectedDatabase = $"NeoSTP_{environment}";

        config["Deployment:EnvironmentId"].Should().Be(expectedId);
        config["Deployment:DataRoot"].Should().EndWith($"/{environment}");
        new SqlConnectionStringBuilder(config.GetConnectionString("NeoStpDb"))
            .InitialCatalog.Should().Be(expectedDatabase);
        config["DataProtection:KeyRingPath"].Should().StartWith(config["Deployment:DataRoot"]!);
        config["DataProtection:CertificateThumbprint"].Should().BeEmpty(
            "the deployment must inject its own certificate thumbprint");

        foreach (var key in new[]
                 {
                     "Ops:Database:ApplyMigrationsOnStartup",
                     "Ops:Database:SeedOnStartup",
                     "SuperAdmin:BootstrapEnabled",
                     "EmpresaPrueba:Enabled",
                     "DemoComercial:Enabled"
                 })
            config.GetValue<bool>(key).Should().BeFalse(key);

        foreach (var key in new[]
                 {
                     "Email:Provider",
                     "Billing:Provider",
                     "Scan:Provider",
                     "WhatsApp:Provider",
                     "Push:Provider"
                 })
            config[key].Should().Be("Disabled", key);

        config["Hacienda:Client"].Should().Be("Http");
        config["Dte:Signer"].Should().Be("HaciendaCert");

        var fileSink = config.GetSection("Serilog:WriteTo").GetChildren()
            .Single(section => section["Name"] == "File");
        fileSink["Args:path"].Should().StartWith(config["Deployment:DataRoot"]!);

        if (host == "NeoSTP.Worker")
        {
            config.GetValue<bool>("Worker:Enabled").Should().BeFalse();
            config["Hardening:Backup:LocalPath"].Should().StartWith(config["Deployment:DataRoot"]!);
        }
    }

    [Theory]
    [InlineData("NeoSTP.Api", "Staging", "staging-api.neostp.com", "https://staging.neostp.com")]
    [InlineData("NeoSTP.Api", "Production", "api.neostp.com", "https://app.neostp.com")]
    [InlineData("NeoSTP.Web", "Staging", "staging.neostp.com", "https://staging-api.neostp.com")]
    [InlineData("NeoSTP.Web", "Production", "app.neostp.com", "https://api.neostp.com")]
    public void PublicEndpointOverlay_UsesOnlyItsEnvironment(
        string host,
        string environment,
        string expectedHost,
        string expectedPeer)
    {
        var config = Load(host, environment);
        config["AllowedHosts"].Should().Be(expectedHost);
        if (host == "NeoSTP.Api")
            config.GetSection("Cors:AllowedOrigins").Get<string[]>().Should().Equal(expectedPeer);
        else
            config["ApiBaseUrl"].Should().Be(expectedPeer);
    }

    [Theory]
    [InlineData("NeoSTP.Api")]
    [InlineData("NeoSTP.Web")]
    [InlineData("NeoSTP.Worker")]
    public void StagingAndProduction_DoNotShareDatabaseDataRootKeyRingLogsOrBackup(string host)
    {
        var staging = Load(host, "Staging");
        var production = Load(host, "Production");
        foreach (var key in new[]
                 {
                     "ConnectionStrings:NeoStpDb",
                     "Deployment:DataRoot",
                     "DataProtection:KeyRingPath",
                     "Serilog:WriteTo:1:Args:path"
                 })
            staging[key].Should().NotBe(production[key], key);

        if (host == "NeoSTP.Worker")
            staging["Hardening:Backup:LocalPath"].Should().NotBe(production["Hardening:Backup:LocalPath"]);
    }

    private static IConfigurationRoot Load(string host, string environment)
    {
        var root = FindRepoRoot();
        return new ConfigurationBuilder()
            .SetBasePath(root)
            .AddJsonFile(Path.Combine("src", host, $"appsettings.{environment}.json"), optional: false)
            .Build();
    }

    private static string FindRepoRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "NeoSTP.slnx")))
                return directory.FullName;

        throw new DirectoryNotFoundException("No se encontró la raíz del repositorio.");
    }
}
