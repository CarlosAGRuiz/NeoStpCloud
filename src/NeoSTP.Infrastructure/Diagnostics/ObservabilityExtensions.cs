using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace NeoSTP.Infrastructure.Diagnostics;

/// <summary>
/// V2.5-S3 — OpenTelemetry opcional por configuración. Sin
/// <c>Observability:Otlp:Endpoint</c> no se monta el pipeline (cero overhead,
/// comportamiento V2); con endpoint, exporta trazas y métricas por OTLP:
/// ASP.NET Core + HttpClient + runtime + el Meter de negocio "NeoSTP".
/// </summary>
public static class ObservabilityExtensions
{
    public static IServiceCollection AddNeoStpObservability(
        this IServiceCollection services,
        IConfiguration configuration,
        string serviceName)
    {
        // El Meter de negocio siempre está disponible (no-op si nadie escucha).
        services.AddSingleton<NeoStpMetrics>();

        var endpoint = configuration["Observability:Otlp:Endpoint"];
        if (string.IsNullOrWhiteSpace(endpoint)) return services;

        var otlpUri = new Uri(endpoint);
        services.AddOpenTelemetry()
            .ConfigureResource(r => r.AddService(
                serviceName,
                serviceVersion: typeof(ObservabilityExtensions).Assembly.GetName().Version?.ToString(),
                serviceInstanceId: Environment.MachineName))
            .WithTracing(t => t
                .AddAspNetCoreInstrumentation(o =>
                {
                    // No trazar el ruido de health checks.
                    o.Filter = ctx => !ctx.Request.Path.StartsWithSegments("/health");
                })
                .AddHttpClientInstrumentation()
                .AddOtlpExporter(o => o.Endpoint = otlpUri))
            .WithMetrics(m => m
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation()
                .AddMeter(NeoStpMetrics.MeterName)
                .AddOtlpExporter(o => o.Endpoint = otlpUri));

        return services;
    }
}
