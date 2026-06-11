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
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default)
    {
        try
        {
            var dir = Path.Combine(AppContext.BaseDirectory, "logs");
            Directory.CreateDirectory(dir);
            var probe = Path.Combine(dir, $".health-{Guid.NewGuid():N}.tmp");
            File.WriteAllText(probe, "ok");
            File.Delete(probe);
            return Task.FromResult(HealthCheckResult.Healthy("Almacenamiento local escribible."));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy($"No se puede escribir en disco: {ex.Message}"));
        }
    }
}
