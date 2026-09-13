using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace NeoSTP.Infrastructure.Diagnostics;

/// <summary>
/// Activation policy for worker jobs. Deployed environments require both the master
/// switch and a per-job switch; Development keeps opt-out defaults for local convenience.
/// </summary>
public static class WorkerStartupPolicy
{
    public static void ConfigureJobs(IServiceCollection services, IConfiguration configuration,
        IHostEnvironment environment, Action<IServiceCollection> registerJobs)
    {
        if (ReadFlag(configuration, "Worker:Enabled", !environment.IsProduction()))
            registerJobs(services);
    }

    public static void ConfigureJob(
        IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment,
        string jobName,
        Action<IServiceCollection> registerJob)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(jobName);
        ArgumentNullException.ThrowIfNull(registerJob);

        var masterEnabled = ReadFlag(
            configuration,
            "Worker:Enabled",
            environment.IsDevelopment());

        if (!masterEnabled)
            return;

        var jobEnabled = ReadFlag(
            configuration,
            $"Worker:{jobName}:Enabled",
            environment.IsDevelopment());

        if (jobEnabled)
            registerJob(services);
    }

    public static bool IsJobEnabled(
        IConfiguration configuration,
        IHostEnvironment environment,
        string jobName)
    {
        var enabled = false;
        ConfigureJob(new ServiceCollection(), configuration, environment, jobName, _ => enabled = true);
        return enabled;
    }

    private static bool ReadFlag(IConfiguration configuration, string key, bool defaultValue)
    {
        var configured = configuration[key];
        if (configured is null)
            return defaultValue;
        if (bool.TryParse(configured, out var enabled))
            return enabled;

        throw new InvalidOperationException(
            $"WORKER_STARTUP_CONFIGURATION_INVALID: {key} must be true or false.");
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
