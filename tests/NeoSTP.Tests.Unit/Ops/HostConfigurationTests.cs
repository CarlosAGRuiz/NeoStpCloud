using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using NeoSTP.Infrastructure.Diagnostics;
using NSubstitute;
using System.Text.Json;

namespace NeoSTP.Tests.Unit.Ops;

public sealed class HostConfigurationTests
{
    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public void Deployed_environments_never_read_local_file_even_if_it_is_malformed(string name)
    {
        using var files = new ConfigFiles("not-json-and-must-not-be-read");
        using var configuration = files.Configuration();
        var originalSources = configuration.Sources.ToArray();
        HostConfiguration.AddLocalDevelopmentSettings(configuration, files.Environment(name));
        configuration["Setting"].Should().Be("base");
        configuration.Sources.Should().Equal(originalSources);
    }

    [Fact]
    public void Development_local_overrides_base_but_operator_environment_and_command_line_keep_priority()
    {
        using var files = new ConfigFiles("{\"Setting\":\"local\",\"LocalOnly\":\"present\"}");
        var prefix = "NEOSTP_TEST_" + Guid.NewGuid().ToString("N") + "_";
        System.Environment.SetEnvironmentVariable(prefix + "Setting", "operator-environment");
        try
        {
            using var configuration = files.Configuration();
            configuration.AddEnvironmentVariables(prefix);
            HostConfiguration.AddLocalDevelopmentSettings(configuration, files.Environment("Development"));
            configuration["LocalOnly"].Should().Be("present");
            configuration["Setting"].Should().Be("operator-environment");
            configuration.AddCommandLine(new[] { "--Setting=operator-cli" });
            configuration["Setting"].Should().Be("operator-cli");
        }
        finally { System.Environment.SetEnvironmentVariable(prefix + "Setting", null); }
    }

    [Fact]
    public void Development_local_overrides_regular_appsettings_without_replacing_higher_priority_user_secrets()
    {
        using var files = new ConfigFiles("{\"Setting\":\"local\",\"LocalOnly\":\"present\"}");
        using var configuration = files.Configuration();
        HostConfiguration.AddLocalDevelopmentSettings(configuration, files.Environment("Development"));
        configuration["Setting"].Should().Be("local");
        using var secretsConfiguration = files.Configuration();
        secretsConfiguration.AddInMemoryCollection(new Dictionary<string, string?> { ["Setting"] = "synthetic-user-secret" });
        HostConfiguration.AddLocalDevelopmentSettings(secretsConfiguration, files.Environment("Development"));
        secretsConfiguration["Setting"].Should().Be("synthetic-user-secret");
    }

    [Fact]
    public void Production_external_config_overrides_base_but_not_operator_sources()
    {
        using var files = new ExternalConfigFiles("{\"Setting\":\"external\",\"ExternalOnly\":\"present\"}");
        using var configuration = files.Configuration();
        configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["Setting"] = "operator" });

        HostConfiguration.AddExternalDeploymentSettings(
            configuration, files.Environment("Production"), files.ExternalPath);

        configuration["ExternalOnly"].Should().Be("present");
        configuration["Setting"].Should().Be("operator");
    }

    [Fact]
    public void Production_external_config_can_be_selected_by_operator_configuration()
    {
        using var files = new ExternalConfigFiles("{\"ExternalOnly\":\"present\"}");
        using var configuration = files.Configuration();
        configuration.AddCommandLine(
            [$"--{HostConfiguration.ExternalConfigConfigurationKey}={files.ExternalPath}"]);

        HostConfiguration.AddExternalDeploymentSettings(
            configuration, files.Environment("Production"));

        configuration["ExternalOnly"].Should().Be("present");
    }

    [Fact]
    public void External_config_must_be_below_the_environment_config_directory()
    {
        using var files = new ExternalConfigFiles("{\"Setting\":\"external\"}");
        var outside = files.WriteOutside("{\"Setting\":\"outside\"}");
        using var configuration = files.Configuration();

        var action = () => HostConfiguration.AddExternalDeploymentSettings(
            configuration, files.Environment("Production"), outside);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("EXTERNAL_DEPLOYMENT_CONFIG_INVALID:*");
    }

    [Fact]
    public void Development_rejects_external_deployment_config()
    {
        using var files = new ExternalConfigFiles("{\"Setting\":\"external\"}");
        using var configuration = files.Configuration();

        var action = () => HostConfiguration.AddExternalDeploymentSettings(
            configuration, files.Environment("Development"), files.ExternalPath);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("EXTERNAL_DEPLOYMENT_CONFIG_INVALID:*");
    }

    private sealed class ExternalConfigFiles : IDisposable
    {
        private readonly string root =
            Path.Combine(Path.GetTempPath(), "NeoSTP-ExternalHostConfiguration-" + Guid.NewGuid().ToString("N"));
        private readonly string contentRoot;
        private readonly PhysicalFileProvider provider;

        public ExternalConfigFiles(string external)
        {
            contentRoot = Path.Combine(root, "content");
            DataRoot = Path.Combine(root, "PRODUCTION");
            ExternalPath = Path.Combine(DataRoot, "config", "runtime.json");
            Directory.CreateDirectory(contentRoot);
            Directory.CreateDirectory(Path.GetDirectoryName(ExternalPath)!);
            File.WriteAllText(Path.Combine(contentRoot, "appsettings.json"),
                JsonSerializer.Serialize(new { Setting = "base", Deployment = new { DataRoot } }));
            File.WriteAllText(ExternalPath, external);
            provider = new PhysicalFileProvider(contentRoot);
        }

        public string DataRoot { get; }
        public string ExternalPath { get; }

        public ConfigurationManager Configuration()
        {
            var configuration = new ConfigurationManager();
            configuration.AddJsonFile(provider, "appsettings.json", false, false);
            return configuration;
        }

        public IHostEnvironment Environment(string name)
        {
            var environment = Substitute.For<IHostEnvironment>();
            environment.EnvironmentName.Returns(name);
            environment.ContentRootPath.Returns(contentRoot);
            environment.ContentRootFileProvider.Returns(provider);
            return environment;
        }

        public string WriteOutside(string json)
        {
            var path = Path.Combine(root, "outside.json");
            File.WriteAllText(path, json);
            return path;
        }

        public void Dispose()
        {
            provider.Dispose();
            Directory.Delete(root, true);
        }
    }

    private sealed class ConfigFiles : IDisposable
    {
        private readonly string directory = Path.Combine(Path.GetTempPath(), "NeoSTP-HostConfiguration-" + Guid.NewGuid().ToString("N"));
        private readonly PhysicalFileProvider provider;
        public ConfigFiles(string local)
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, "appsettings.json"), "{\"Setting\":\"base\"}");
            File.WriteAllText(Path.Combine(directory, "appsettings.Local.json"), local);
            provider = new PhysicalFileProvider(directory);
        }
        public ConfigurationManager Configuration()
        {
            var configuration = new ConfigurationManager();
            configuration.AddJsonFile(provider, "appsettings.json", false, false);
            return configuration;
        }
        public IHostEnvironment Environment(string name)
        {
            var environment = Substitute.For<IHostEnvironment>();
            environment.EnvironmentName.Returns(name); environment.ContentRootFileProvider.Returns(provider);
            return environment;
        }
        public void Dispose() { provider.Dispose(); Directory.Delete(directory, true); }
    }
}