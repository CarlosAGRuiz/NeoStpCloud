using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NeoSTP.Infrastructure.Diagnostics;
using NeoSTP.Infrastructure.Persistence;
using NSubstitute;

namespace NeoSTP.Tests.Unit.Ops;

/// <summary>Real EF migration discovery with synthetic history/migrator services; no SQL connection or real database.</summary>
public sealed class DatabaseStartupTests
{
    [Theory]
    [InlineData("Ops:Database:ApplyMigrationsOnStartup")]
    [InlineData("Ops:Database:SeedOnStartup")]
    [InlineData("EmpresaPrueba:Enabled")]
    [InlineData("DemoComercial:Enabled")]
    [InlineData("SuperAdmin:BootstrapEnabled")]
    public async Task Production_write_flag_is_rejected_before_any_service_or_database_access(string flag)
    {
        var services = Substitute.For<IServiceProvider>();
        var config = Configuration((flag, "true"));
        var action = () => DatabaseStartup.InitializeAsync(services, config, Environment(Environments.Production));
        await action.Should().ThrowAsync<InvalidOperationException>().WithMessage("DATABASE_STARTUP_WRITES_FORBIDDEN:*");
        services.ReceivedCalls().Should().BeEmpty();
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    [InlineData("SyntheticCustomEnvironment")]
    public async Task Nondevelopment_defaults_validate_current_schema_without_migration_or_seed_writes(string environment)
    {
        using var fixture = new HistoryFixture();
        fixture.MarkAllApplied();

        await DatabaseStartup.InitializeAsync(fixture.Services, Configuration(), Environment(environment));

        await fixture.History.Received(1).GetAppliedMigrationsAsync(Arg.Any<CancellationToken>());
        fixture.AssertNoWrites();
    }

    [Fact]
    public async Task Development_explicit_false_flags_validate_only_without_bootstrap_dependencies()
    {
        using var fixture = new HistoryFixture(); fixture.MarkAllApplied();
        var config = Configuration(("Ops:Database:ApplyMigrationsOnStartup", "false"), ("Ops:Database:SeedOnStartup", "false"));
        await DatabaseStartup.InitializeAsync(fixture.Services, config, Environment(Environments.Development));
        fixture.AssertNoWrites();
        await fixture.History.Received(1).GetAppliedMigrationsAsync(Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Missing_or_old_schema_blocks_production_without_applying_pending_migrations(bool partiallyApplied)
    {
        using var fixture = new HistoryFixture();
        if (partiallyApplied) fixture.Rows = fixture.MigrationIds.Take(fixture.MigrationIds.Length - 1).Select(HistoryRow).ToArray();
        var action = () => DatabaseStartup.InitializeAsync(fixture.Services, Configuration(), Environment(Environments.Production));
        await action.Should().ThrowAsync<InvalidOperationException>().WithMessage("DATABASE_SCHEMA_NOT_READY:*");
        fixture.AssertNoWrites();
    }

    [Fact]
    public async Task Future_or_foreign_applied_migration_blocks_startup_without_writing_or_rereading_history()
    {
        using var fixture = new HistoryFixture(); fixture.MarkAllApplied();
        fixture.Rows = fixture.Rows.Append(new HistoryRow("99991231235959_SyntheticFutureMigration", "99.0.0")).ToArray();
        var action = () => DatabaseStartup.InitializeAsync(fixture.Services, Configuration(), Environment(Environments.Production));

        await action.Should().ThrowAsync<InvalidOperationException>().WithMessage("DATABASE_SCHEMA_NOT_READY:*");

        await fixture.History.Received(1).GetAppliedMigrationsAsync(Arg.Any<CancellationToken>());
        fixture.AssertNoWrites();
    }
    [Fact]
    public async Task Database_failure_is_sanitized_without_inner_exception_or_connection_details()
    {
        using var fixture = new HistoryFixture();
        const string sensitive = "synthetic-password-and-server-never-expose";
        fixture.History.GetAppliedMigrationsAsync(Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromException<IReadOnlyList<HistoryRow>>(new InvalidOperationException(sensitive)));
        var action = () => DatabaseStartup.InitializeAsync(fixture.Services, Configuration(), Environment(Environments.Production));
        var thrown = await action.Should().ThrowAsync<InvalidOperationException>();
        thrown.Which.Message.Should().StartWith("DATABASE_SCHEMA_NOT_READY:");
        thrown.Which.ToString().Should().NotContain(sensitive);
        thrown.Which.InnerException.Should().BeNull();
        fixture.AssertNoWrites();
    }

    [Fact]
    public async Task Malformed_flag_does_not_expose_supplied_value_or_resolve_database()
    {
        var services = Substitute.For<IServiceProvider>();
        const string sensitive = "synthetic-secret-not-a-bool";
        var action = () => DatabaseStartup.InitializeAsync(services,
            Configuration(("Ops:Database:SeedOnStartup", sensitive)), Environment(Environments.Production));
        var thrown = await action.Should().ThrowAsync<InvalidOperationException>();
        thrown.Which.ToString().Should().NotContain(sensitive);
        thrown.Which.Message.Should().StartWith("DATABASE_STARTUP_CONFIGURATION_INVALID:");
        services.ReceivedCalls().Should().BeEmpty();
    }

    [Fact]
    public void Development_defaults_preserve_migration_and_bootstrap_while_demo_provisioning_stays_opt_in()
    {
        var policy = DatabaseStartup.GetPolicy(Configuration(), Environment(Environments.Development));
        policy.ApplyMigrations.Should().BeTrue(); policy.Seed.Should().BeTrue(); policy.Bootstrap.Should().BeTrue();
        policy.SeedCompany.Should().BeFalse(); policy.SeedDemo.Should().BeFalse();
    }

    [Fact]
    public void Explicit_staging_flags_can_opt_in_without_changing_production_defaults()
    {
        var policy = DatabaseStartup.GetPolicy(Configuration(("Ops:Database:ApplyMigrationsOnStartup", "true"),
            ("Ops:Database:SeedOnStartup", "true"), ("SuperAdmin:BootstrapEnabled", "true")), Environment(Environments.Staging));
        policy.ApplyMigrations.Should().BeTrue(); policy.Seed.Should().BeTrue(); policy.Bootstrap.Should().BeTrue();
        var production = DatabaseStartup.GetPolicy(Configuration(), Environment(Environments.Production));
        production.ApplyMigrations.Should().BeFalse(); production.Seed.Should().BeFalse(); production.Bootstrap.Should().BeFalse();
    }

    [Fact]
    public async Task Requested_cancellation_does_not_access_database_or_turn_into_schema_failure()
    {
        var services = Substitute.For<IServiceProvider>();
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        var action = () => DatabaseStartup.InitializeAsync(services, Configuration(), Environment(Environments.Production), cancellation.Token);
        await action.Should().ThrowAsync<OperationCanceledException>();
        services.ReceivedCalls().Should().BeEmpty();
    }

    private static HistoryRow HistoryRow(string id) => new(id, "10.0.10");
    private static IConfiguration Configuration(params (string Key, string Value)[] flags)
        => new ConfigurationBuilder().AddInMemoryCollection(flags.Select(x => new KeyValuePair<string, string?>(x.Key, x.Value))).Build();
    private static IHostEnvironment Environment(string name)
    {
        var environment = Substitute.For<IHostEnvironment>(); environment.EnvironmentName.Returns(name); return environment;
    }
    private sealed class HistoryFixture : IDisposable
    {
        private readonly ServiceProvider _efServices;
        public ServiceProvider Services { get; }
        public IHistoryRepository History { get; } = Substitute.For<IHistoryRepository>();
        public IMigrator Migrator { get; } = Substitute.For<IMigrator>();
        public SaveProbe Saves { get; } = new();
        public IReadOnlyList<HistoryRow> Rows { get; set; } = [];
        public string[] MigrationIds { get; }
        public HistoryFixture()
        {
            History.GetAppliedMigrationsAsync(Arg.Any<CancellationToken>()).Returns(_ => Task.FromResult(Rows));
            _efServices = new ServiceCollection().AddEntityFrameworkSqlServer().AddSingleton(History).AddSingleton(Migrator).BuildServiceProvider();
            var services = new ServiceCollection();
            services.AddDbContext<NeoStpDbContext>(options => options
                .UseSqlServer("Server=synthetic.invalid;Database=NeverConnect;Integrated Security=True;TrustServerCertificate=True")
                .UseInternalServiceProvider(_efServices).AddInterceptors(Saves));
            Services = services.BuildServiceProvider();
            using var scope = Services.CreateScope();
            MigrationIds = scope.ServiceProvider.GetRequiredService<NeoStpDbContext>().Database.GetMigrations().ToArray();
            MigrationIds.Should().NotBeEmpty();
        }
        public void MarkAllApplied() => Rows = MigrationIds.Select(HistoryRow).ToArray();
        public void AssertNoWrites() { Migrator.ReceivedCalls().Should().BeEmpty(); Saves.Calls.Should().Be(0); }
        public void Dispose() { Services.Dispose(); _efServices.Dispose(); }
    }
    private sealed class SaveProbe : SaveChangesInterceptor
    {
        public int Calls { get; private set; }
        public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
        { Calls++; return result; }
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken ct = default)
        { Calls++; return ValueTask.FromResult(result); }
    }
}
