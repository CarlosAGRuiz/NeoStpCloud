using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.DependencyInjection;

namespace NeoSTP.Infrastructure.Diagnostics;

/// <summary>
/// Health checks de NeoSTP (M3.2). Expone:
///   - <c>/health/live</c>  liveness: el proceso responde (sin dependencias).
///   - <c>/health/ready</c> readiness: dependencias críticas (BD) accesibles.
/// </summary>
public static class HealthCheckExtensions
{
    public const string ReadyTag = "ready";

    public static IServiceCollection AddNeoStpHealthChecks(this IServiceCollection services)
    {
        services.AddHealthChecks()
            .AddCheck<DbHealthCheck>("database", tags: new[] { ReadyTag });
        return services;
    }

    public static IEndpointRouteBuilder MapNeoStpHealthChecks(this IEndpointRouteBuilder endpoints)
    {
        // Liveness: no ejecuta ningún check (solo confirma que el host responde).
        endpoints.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = _ => false,
            ResponseWriter = WriteResponse,
        });

        // Readiness: ejecuta los checks etiquetados "ready" (BD).
        endpoints.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains(ReadyTag),
            ResponseWriter = WriteResponse,
        });

        return endpoints;
    }

    private static Task WriteResponse(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json; charset=utf-8";
        var payload = new
        {
            status = report.Status.ToString(),
            totalDurationMs = report.TotalDuration.TotalMilliseconds,
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                description = e.Value.Description,
                durationMs = e.Value.Duration.TotalMilliseconds,
            }),
        };
        return context.Response.WriteAsync(JsonSerializer.Serialize(payload));
    }
}
