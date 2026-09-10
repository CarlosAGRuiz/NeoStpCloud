using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NeoSTP.Shared;

namespace NeoSTP.Infrastructure.Auth;

/// <summary>Per-process authentication limits, independent from business/API-key quotas.</summary>
public static class AuthRateLimiting
{
    public const string Login = "auth-login";
    public const string Mfa = "auth-mfa";
    public const string Refresh = "auth-refresh";

    public static IServiceCollection AddAuthRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        var settings = configuration.GetSection("Security:AuthRateLimit").Get<AuthRateLimitSettings>() ?? new();
        settings.Validate();
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = async (context, ct) =>
            {
                var response = context.HttpContext.Response;
                var seconds = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retry)
                    ? Math.Max(1, (int)Math.Ceiling(retry.TotalSeconds)) : settings.WindowSeconds;
                response.Headers.RetryAfter = seconds.ToString(CultureInfo.InvariantCulture);
                response.Headers.CacheControl = "no-store";
                const string message = "Demasiados intentos. Espera unos momentos antes de volver a intentarlo.";
                if (context.HttpContext.Request.Path.StartsWithSegments("/api"))
                    await response.WriteAsJsonAsync(ApiResponse.Fail(message, traceId: context.HttpContext.TraceIdentifier), ct);
                else
                {
                    response.ContentType = "text/html; charset=utf-8";
                    await response.WriteAsync("<!doctype html><html lang=\"es\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\"><title>Espera antes de reintentar</title><main><h1>Demasiados intentos</h1><p>" + message + "</p><p><a href=\"/Account/Login\">Volver al inicio de sesión</a></p></main></html>", ct);
                }
            };
            Add(Login, settings.LoginPermits);
            Add(Mfa, settings.MfaPermits);
            Add(Refresh, settings.RefreshPermits);
            void Add(string policy, int permits) =>
                options.AddPolicy(policy, context => RateLimitPartition.GetFixedWindowLimiter(
                    GetClientPartition(context), _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = permits, Window = TimeSpan.FromSeconds(settings.WindowSeconds),
                        QueueLimit = 0, AutoReplenishment = true
                    }));
        });
        return services;
    }

    internal static string GetClientPartition(HttpContext context)
    {
        // ForwardedHeaders middleware validates the proxy first. Never trust raw headers here.
        var ip = context.Connection.RemoteIpAddress;
        return ip?.IsIPv4MappedToIPv6 == true ? ip.MapToIPv4().ToString() : ip?.ToString() ?? "unknown";
    }
}

public sealed class AuthRateLimitSettings
{
    public int WindowSeconds { get; set; } = 60;
    public int LoginPermits { get; set; } = 10;
    public int MfaPermits { get; set; } = 10;
    public int RefreshPermits { get; set; } = 30;

    internal void Validate()
    {
        if (WindowSeconds is < 1 or > 3600 || LoginPermits is < 1 or > 1000
            || MfaPermits is < 1 or > 1000 || RefreshPermits is < 1 or > 1000)
            throw new InvalidOperationException("Security:AuthRateLimit requiere una ventana de 1–3600 segundos y límites de 1–1000.");
    }
}
