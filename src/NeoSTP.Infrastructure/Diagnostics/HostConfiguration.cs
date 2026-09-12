using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace NeoSTP.Infrastructure.Diagnostics;

/// <summary>Local development values never participate in a deployed environment or override operator settings.</summary>
public static class HostConfiguration
{
    public const string ExternalConfigEnvironmentVariable = "NEOSTP_EXTERNAL_CONFIG_FILE";
    public const string ExternalConfigConfigurationKey = "Deployment:ExternalConfigFile";
    private const string ExternalConfigFailure =
        "EXTERNAL_DEPLOYMENT_CONFIG_INVALID: configure an existing JSON file below the environment data-root config directory.";

    public static string? GetExternalDeploymentPath(string[] args)
    {
        var option = $"--{ExternalConfigConfigurationKey}";
        var prefix = option + "=";
        string? resolved = null;

        for (var index = 0; index < args.Length; index++)
        {
            var argument = args[index];
            if (argument.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                resolved = argument[prefix.Length..];
                continue;
            }

            if (string.Equals(argument, option, StringComparison.OrdinalIgnoreCase)
                && index + 1 < args.Length)
                resolved = args[++index];
        }

        return resolved;
    }

    public static void AddExternalDeploymentSettings(
        ConfigurationManager configuration,
        IHostEnvironment environment,
        string? externalPath = null)
    {
        externalPath ??= Environment.GetEnvironmentVariable(ExternalConfigEnvironmentVariable);
        externalPath ??= configuration[ExternalConfigConfigurationKey];
        if (string.IsNullOrWhiteSpace(externalPath)) return;

        try
        {
            if (!environment.IsProduction() && !environment.IsStaging())
                throw new InvalidOperationException();

            var dataRootValue = configuration["Deployment:DataRoot"];
            if (string.IsNullOrWhiteSpace(dataRootValue) || !Path.IsPathFullyQualified(dataRootValue)
                || !Path.IsPathFullyQualified(externalPath))
                throw new InvalidOperationException();

            var comparison = OperatingSystem.IsWindows()
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;
            var dataRoot = Path.GetFullPath(dataRootValue)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var configRoot = Path.Combine(dataRoot, "config");
            var fullPath = Path.GetFullPath(externalPath);

            if (!string.Equals(Path.GetExtension(fullPath), ".json", comparison)
                || !File.Exists(fullPath)
                || !IsChildPath(fullPath, configRoot, comparison)
                || (File.GetAttributes(fullPath) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidOperationException();

            var contentRootValue = environment.ContentRootPath;
            if (!string.IsNullOrWhiteSpace(contentRootValue) && Path.IsPathFullyQualified(contentRootValue))
            {
                var contentRoot = Path.GetFullPath(contentRootValue)
                    .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                if (string.Equals(fullPath, contentRoot, comparison)
                    || IsChildPath(fullPath, contentRoot, comparison))
                    throw new InvalidOperationException();
            }

            for (var directory = new DirectoryInfo(Path.GetDirectoryName(fullPath)!);
                 directory is not null;
                 directory = directory.Parent)
            {
                if ((directory.Attributes & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidOperationException();
                if (string.Equals(directory.FullName.TrimEnd(Path.DirectorySeparatorChar),
                    configRoot.TrimEnd(Path.DirectorySeparatorChar), comparison))
                    break;
            }

            var position = 0;
            for (var index = 0; index < configuration.Sources.Count; index++)
                if (configuration.Sources[index] is JsonConfigurationSource source
                    && (string.Equals(source.Path, "appsettings.json", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(source.Path, $"appsettings.{environment.EnvironmentName}.json",
                            StringComparison.OrdinalIgnoreCase)))
                    position = index + 1;

            configuration.Sources.Insert(position, new JsonConfigurationSource
            {
                FileProvider = new PhysicalFileProvider(Path.GetDirectoryName(fullPath)!),
                Path = Path.GetFileName(fullPath),
                Optional = false,
                ReloadOnChange = false
            });
        }
        catch (Exception)
        {
            throw new InvalidOperationException(ExternalConfigFailure);
        }
    }

    private static bool IsChildPath(string path, string root, StringComparison comparison)
    {
        var normalizedRoot = Path.GetFullPath(root)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var normalizedPath = Path.GetFullPath(path);
        var relative = Path.GetRelativePath(normalizedRoot, normalizedPath);
        return relative.Length > 0
            && relative != "."
            && relative != ".."
            && !relative.StartsWith($"..{Path.DirectorySeparatorChar}", comparison)
            && !Path.IsPathFullyQualified(relative);
    }

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