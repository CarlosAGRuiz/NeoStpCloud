using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace NeoSTP.Infrastructure.Diagnostics;

/// <summary>
/// V2.5-S3 — readiness ampliado: configuración de correo coherente. No abre conexión
/// SMTP en cada sondeo (eso castigaría al proveedor); valida que el proveedor activo
/// tenga lo mínimo para operar. Devuelve Degraded, no Unhealthy: un correo mal
/// configurado no debe sacar la instancia del balanceador.
/// </summary>
public sealed class EmailConfigHealthCheck : IHealthCheck
{
    private readonly IConfiguration _configuration;

    public EmailConfigHealthCheck(IConfiguration configuration) => _configuration = configuration;

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default)
    {
        var provider = _configuration["Email:Provider"] ?? "Mock";
        if (!string.Equals(provider, "Smtp", StringComparison.OrdinalIgnoreCase))
            return Task.FromResult(HealthCheckResult.Healthy($"Proveedor de correo: {provider}."));

        var host = _configuration["Email:Smtp:Host"];
        var faltantes = new List<string>();
        if (string.IsNullOrWhiteSpace(host) || host == "localhost") faltantes.Add("Email:Smtp:Host");
        if (string.IsNullOrWhiteSpace(_configuration["Email:From:Address"])) faltantes.Add("Email:From:Address");

        return Task.FromResult(faltantes.Count == 0
            ? HealthCheckResult.Healthy($"SMTP global configurado ({host}).")
            : HealthCheckResult.Degraded($"SMTP activo pero incompleto: falta {string.Join(", ", faltantes)}."));
    }
}

/// <summary>
/// V2.5-S3 — readiness ampliado: el directorio de trabajo (logs/salida) es escribible.
/// Detecta discos llenos o permisos rotos antes de que fallen los flujos reales.
/// </summary>
public sealed class StorageHealthCheck : IHealthCheck
{
    private readonly IConfiguration _configuration;

    public StorageHealthCheck(IConfiguration configuration) => _configuration = configuration;

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default)
    {
        try
        {
            var probes = new List<string>
            {
                ProbeDirectory(Path.Combine(AppContext.BaseDirectory, "logs"), "logs"),
            };

            var provider = _configuration["Scan:Storage:Provider"] ?? "Database";
            if (string.Equals(provider, "FileSystem", StringComparison.OrdinalIgnoreCase))
            {
                var root = ResolvePath(_configuration["Scan:Storage:Root"] ?? "scan-blobs");
                probes.Add(ProbeDirectory(root, "Scan:Storage:Root"));
            }
            else if (!string.Equals(provider, "Database", StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(HealthCheckResult.Unhealthy(
                    $"Scan:Storage:Provider '{provider}' no soportado. Use Database o FileSystem."));
            }

            return Task.FromResult(HealthCheckResult.Healthy(
                $"Storage OK ({provider}). {string.Join("; ", probes)}."));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy($"No se puede escribir en disco: {ex.Message}"));
        }
    }

    private static string ResolvePath(string path) =>
        Path.IsPathRooted(path) ? path : Path.Combine(AppContext.BaseDirectory, path);

    private static string ProbeDirectory(string dir, string label)
    {
        Directory.CreateDirectory(dir);
        var probe = Path.Combine(dir, $".health-{Guid.NewGuid():N}.tmp");
        File.WriteAllText(probe, "ok");
        File.Delete(probe);
        return $"{label} escribible";
    }
}
