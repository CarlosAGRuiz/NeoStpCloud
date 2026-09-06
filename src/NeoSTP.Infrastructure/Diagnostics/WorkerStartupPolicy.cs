using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace NeoSTP.Infrastructure.Diagnostics;

/// <summary>One explicit production activation gate for all worker jobs and separate read-only database defaults.</summary>
public static class WorkerStartupPolicy
{
    public static void ConfigureJobs(IServiceCollection services, IConfiguration configuration,
        IHostEnvironment environment, Action<IServiceCollection> registerJobs)
    {
        var configured = configuration["Worker:Enabled"];
        var enabled = !environment.IsProduction();
        if (configured is not null && !bool.TryParse(configured, out enabled))
            throw new InvalidOperationException("WORKER_STARTUP_CONFIGURATION_INVALID: Worker:Enabled must be true or false.");
        if (enabled) registerJobs(services);
    }

    /// <summary>Workers do not inherit the Development web/API defaults for database migrations or bootstrap.</summary>
    public static IConfigurationRoot CreateDatabaseConfiguration(IConfiguration configuration)
        => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Ops:Database:ApplyMigrationsOnStartup"] = "false",
            ["Ops:Database:SeedOnStartup"] = "false",
            ["SuperAdmin:BootstrapEnabled"] = "false"
        }).AddConfiguration(configuration).Build();
}
