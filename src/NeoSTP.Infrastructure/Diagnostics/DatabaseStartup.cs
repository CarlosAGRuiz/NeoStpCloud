using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NeoSTP.Application.Auth;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Infrastructure.Persistence;
using NeoSTP.Infrastructure.Persistence.Seed;

namespace NeoSTP.Infrastructure.Diagnostics;

/// <summary>Production startup validates migration history only. Database deployment is a separate operation.</summary>
public static class DatabaseStartup
{
    private const string MigrateKey = "Ops:Database:ApplyMigrationsOnStartup";
    private const string SeedKey = "Ops:Database:SeedOnStartup";
    private const string BootstrapKey = "SuperAdmin:BootstrapEnabled";
    private const string CompanyKey = "EmpresaPrueba:Enabled";
    private const string DemoKey = "DemoComercial:Enabled";

    public static async Task InitializeAsync(IServiceProvider services, IConfiguration configuration,
        IHostEnvironment environment, CancellationToken ct = default)
    {
        var policy = GetPolicy(configuration, environment);
        ct.ThrowIfCancellationRequested();
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<NeoStpDbContext>();
        if (policy.ApplyMigrations)
            await db.Database.MigrateAsync(ct);
        else
            await ValidateSchemaAsync(db, ct);

        if (!policy.Seed) return;
        // DatabaseSeeder.SeedAsync also migrates; use its isolated bootstrap to respect the migration flag.
        if (policy.Bootstrap)
            await DatabaseSeeder.EnsureSuperAdminAsync(db,
                scope.ServiceProvider.GetRequiredService<IPasswordHasher>(),
                scope.ServiceProvider.GetRequiredService<IOptions<SuperAdminOptions>>().Value,
                scope.ServiceProvider.GetRequiredService<ILogger<NeoStpDbContext>>(), ct);
        if (policy.SeedCompany) await EmpresaPruebaSeeder.SeedAsync(services, ct);
        if (policy.SeedDemo) await DemoComercialSeeder.SeedAsync(services, ct);
    }

    internal sealed record StartupPolicy(bool ApplyMigrations, bool Seed, bool Bootstrap, bool SeedCompany, bool SeedDemo);

    internal static StartupPolicy GetPolicy(IConfiguration configuration, IHostEnvironment environment)
    {
        var development = environment.IsDevelopment();
        var migrate = Flag(configuration, MigrateKey, development);
        var seed = Flag(configuration, SeedKey, development);
        var bootstrap = Flag(configuration, BootstrapKey, development);
        var company = Flag(configuration, CompanyKey, false);
        var demo = Flag(configuration, DemoKey, false);
        if (environment.IsProduction() && (migrate || seed || bootstrap || company || demo))
            throw new InvalidOperationException("DATABASE_STARTUP_WRITES_FORBIDDEN: migrations, bootstrap and seed flags must be disabled in Production.");
        return new(migrate, seed, bootstrap, company, demo);
    }

    private static bool Flag(IConfiguration configuration, string key, bool fallback)
    {
        var value = configuration[key];
        if (value is null) return fallback;
        if (bool.TryParse(value, out var enabled)) return enabled;
        // Never include an invalid supplied value; configuration may contain sensitive material.
        throw new InvalidOperationException("DATABASE_STARTUP_CONFIGURATION_INVALID: startup flags must be true or false.");
    }

    private static async Task ValidateSchemaAsync(NeoStpDbContext db, CancellationToken ct)
    {
        try
        {
            if (!db.Database.IsRelational()) throw new InvalidOperationException();
            var known = db.Database.GetMigrations().ToHashSet(StringComparer.Ordinal);
            var applied = (await db.Database.GetAppliedMigrationsAsync(ct)).ToHashSet(StringComparer.Ordinal);
            // A newer or foreign history is as incompatible as pending migrations: never serve it blindly.
            if (known.Count == 0 || !known.SetEquals(applied))
                throw new InvalidOperationException("DATABASE_SCHEMA_INCOMPATIBLE");
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception)
        {
            // Do not expose connection strings, SQL, server names or provider exception details.
            throw new InvalidOperationException("DATABASE_SCHEMA_NOT_READY: verify connectivity and apply the approved migrations before starting the host.");
        }
    }
}
