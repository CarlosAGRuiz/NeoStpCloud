using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using NeoSTP.Infrastructure.Diagnostics;
using NSubstitute;

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