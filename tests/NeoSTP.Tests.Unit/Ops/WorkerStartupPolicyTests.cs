using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NeoSTP.Infrastructure.Diagnostics;
using NSubstitute;

namespace NeoSTP.Tests.Unit.Ops;

public sealed class WorkerStartupPolicyTests
{
    [Theory]
    [InlineData("Production", null, 0)]
    [InlineData("Production", "false", 0)]
    [InlineData("Production", "true", 1)]
    [InlineData("Development", null, 1)]
    [InlineData("Development", "false", 0)]
    [InlineData("Staging", null, 1)]
    public async Task Only_enabled_workers_register_and_start_job_services(string environment, string? enabled, int expectedStarts)
    {
        var probe = new JobProbe();
        var configuration = Configuration(("Worker:Enabled", enabled));
        var registrationCalls = 0;
        using var host = new HostBuilder().UseEnvironment(environment).ConfigureServices((context, services) =>
            WorkerStartupPolicy.ConfigureJobs(services, configuration, context.HostingEnvironment, registration =>
            {
                registrationCalls++;
                registration.AddSingleton<IHostedService>(probe);
            })).Build();

        await host.StartAsync();
        await host.StopAsync();

        registrationCalls.Should().Be(expectedStarts);
        probe.Starts.Should().Be(expectedStarts);
        probe.Stops.Should().Be(expectedStarts);
    }

    [Theory]
    [InlineData("")]
    [InlineData("1")]
    [InlineData("synthetic-sensitive-setting")]
    public void Malformed_activation_rejects_before_registering_any_job_and_does_not_echo_value(string value)
    {
        var configuration = Configuration(("Worker:Enabled", value));
        var registrations = 0;
        var action = () => WorkerStartupPolicy.ConfigureJobs(new ServiceCollection(), configuration,
            Environment("Production"), _ => registrations++);
        var thrown = action.Should().Throw<InvalidOperationException>();
        thrown.Which.Message.Should().Be("WORKER_STARTUP_CONFIGURATION_INVALID: Worker:Enabled must be true or false.");
        thrown.Which.InnerException.Should().BeNull();
        registrations.Should().Be(0);
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Production")]
    [InlineData("Staging")]
    public void Worker_database_defaults_remain_read_only_in_every_environment_without_mutating_original_configuration(string environment)
    {
        var original = Configuration(("Unrelated:Setting", "synthetic-preserved"));
        var effective = WorkerStartupPolicy.CreateDatabaseConfiguration(original);
        var policy = DatabaseStartup.GetPolicy(effective, Environment(environment));
        policy.ApplyMigrations.Should().BeFalse(); policy.Seed.Should().BeFalse(); policy.Bootstrap.Should().BeFalse();
        original["Ops:Database:ApplyMigrationsOnStartup"].Should().BeNull();
        original["Ops:Database:SeedOnStartup"].Should().BeNull();
        original["SuperAdmin:BootstrapEnabled"].Should().BeNull();
        effective["Unrelated:Setting"].Should().Be("synthetic-preserved");
    }

    [Fact]
    public void Explicit_database_flags_override_worker_defaults_but_remain_forbidden_in_production()
    {
        var original = Configuration(("Ops:Database:ApplyMigrationsOnStartup", "true"),
            ("Ops:Database:SeedOnStartup", "true"), ("SuperAdmin:BootstrapEnabled", "true"));
        var effective = WorkerStartupPolicy.CreateDatabaseConfiguration(original);
        var development = DatabaseStartup.GetPolicy(effective, Environment("Development"));
        development.ApplyMigrations.Should().BeTrue(); development.Seed.Should().BeTrue(); development.Bootstrap.Should().BeTrue();
        Action production = () => DatabaseStartup.GetPolicy(effective, Environment("Production"));
        production.Should().Throw<InvalidOperationException>().WithMessage("DATABASE_STARTUP_WRITES_FORBIDDEN:*");
    }


    [Theory]
    [InlineData("Production", "true", null, false)]
    [InlineData("Staging", "true", null, false)]
    [InlineData("Production", "true", "false", false)]
    [InlineData("Production", "true", "true", true)]
    [InlineData("Production", "false", "true", false)]
    [InlineData("Development", null, null, true)]
    [InlineData("Development", null, "false", false)]
    public void Deployed_jobs_require_master_and_individual_activation(
        string environment, string? master, string? job, bool expected)
    {
        var configuration = Configuration(
            ("Worker:Enabled", master),
            ("Worker:NotificationOutbox:Enabled", job));

        WorkerStartupPolicy.IsJobEnabled(
            configuration, Environment(environment), "NotificationOutbox").Should().Be(expected);
    }

    [Fact]
    public void Malformed_individual_activation_is_rejected_without_echoing_its_value()
    {
        var configuration = Configuration(
            ("Worker:Enabled", "true"),
            ("Worker:NotificationOutbox:Enabled", "synthetic-sensitive-setting"));

        var action = () => WorkerStartupPolicy.IsJobEnabled(
            configuration, Environment("Production"), "NotificationOutbox");

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("WORKER_STARTUP_CONFIGURATION_INVALID: Worker:NotificationOutbox:Enabled must be true or false.");
    }

    private static IConfiguration Configuration(params (string Key, string? Value)[] flags)
        => new ConfigurationBuilder().AddInMemoryCollection(flags.Select(x => new KeyValuePair<string, string?>(x.Key, x.Value))).Build();
    private static IHostEnvironment Environment(string name)
    {
        var environment = Substitute.For<IHostEnvironment>(); environment.EnvironmentName.Returns(name); return environment;
    }
    private sealed class JobProbe : IHostedService
    {
        public int Starts { get; private set; }
        public int Stops { get; private set; }
        public Task StartAsync(CancellationToken ct) { Starts++; return Task.CompletedTask; }
        public Task StopAsync(CancellationToken ct) { Stops++; return Task.CompletedTask; }
    }
}
