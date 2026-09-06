using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;
using Microsoft.Extensions.Hosting;

namespace NeoSTP.Infrastructure.Diagnostics;

/// <summary>Local development values never participate in a deployed environment or override operator settings.</summary>
public static class HostConfiguration
{
    public static void AddLocalDevelopmentSettings(ConfigurationManager configuration, IHostEnvironment environment)
    {
        if (!environment.IsDevelopment()) return;
        // Insert after the regular appsettings files, before user secrets, environment and command line.
        // Host bootstrap environment sources can precede JSON; preserve their original positions.
        var position = 0;
        for (var index = 0; index < configuration.Sources.Count; index++)
            if (configuration.Sources[index] is JsonConfigurationSource source
                && (string.Equals(source.Path, "appsettings.json", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(source.Path, $"appsettings.{environment.EnvironmentName}.json", StringComparison.OrdinalIgnoreCase)))
                position = index + 1;
        configuration.Sources.Insert(position, new JsonConfigurationSource
        {
            FileProvider = environment.ContentRootFileProvider,
            Path = "appsettings.Local.json",
            Optional = true,
            ReloadOnChange = false
        });
    }
}