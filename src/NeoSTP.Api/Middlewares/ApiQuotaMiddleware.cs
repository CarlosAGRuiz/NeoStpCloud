using System.Diagnostics;
using Microsoft.Extensions.Options;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Connect;
using NeoSTP.Application.Ops;
using NeoSTP.Shared;

namespace NeoSTP.Api.Middlewares;

/// <summary>
/// Rate limiting / cuotas de API. Para cada petición a /api (autenticada o no):
///   1. Evalúa las cuotas aplicables (<see cref="IApiQuotaService"/>).
///   2. Si se excede alguna, responde HTTP 429 con Retry-After y cabeceras X-RateLimit-*.
///   3. Si pasa, atiende la petición y registra el uso (best-effort) para el conteo.
/// SuperAdmin queda exento de las cuotas de empresa.
/// </summary>
public class ApiQuotaMiddleware
{
    private static readonly string[] BypassPaths =
    {
        "/api/auth/", "/health", "/openapi",
    };

    private readonly RequestDelegate _next;
    private readonly bool _enabled;

    public ApiQuotaMiddleware(RequestDelegate next, IOptions<RateLimitOptions> options)
    {
        _next = next;
        _enabled = options.Value.Enabled;
    }

    public async Task InvokeAsync(HttpContext context, ICurrentUser currentUser, IApiQuotaService quotaService)
    {
        var path = context.Request.Path.Value ?? string.Empty;

        if (!_enabled
            || HttpMethods.IsOptions(context.Request.Method)
            || !path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase)
            || BypassPaths.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
        {
            await _next(context);
            return;
        }

        var modulo = MapModulo(path);

        // Resolver ApiKeyId desde contexto NeoConnect si la petición llegó por API Key.
        var apiKeyCtx = context.Items[ApiKeyAuthMiddleware.ContextItemKey] as ConnectApiKeyContext;

        var ctx = new QuotaContext
        {
            EmpresaId = apiKeyCtx?.EmpresaId ?? currentUser.EmpresaId,
            UsuarioId = apiKeyCtx is null ? currentUser.UserId : null,
            ApiKeyId = apiKeyCtx?.ApiKeyId,
            Modulo = modulo,
            IsSuperAdmin = currentUser.TipoUsuarioCodigo == "SUPERADMIN",
        };

        var ip = context.Connection.RemoteIpAddress?.ToString();
        var decision = await quotaService.EvaluarAsync(ctx, context.RequestAborted);

        if (!decision.Allowed)
        {
            context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            if (decision.RetryAfterSeconds is int retry)
                context.Response.Headers["Retry-After"] = retry.ToString();
            if (decision.Limit is int lim)
                context.Response.Headers["X-RateLimit-Limit"] = lim.ToString();
            context.Response.Headers["X-RateLimit-Remaining"] = "0";

            await context.Response.WriteAsJsonAsync(ApiResponse.Fail(
                "Se excedió el límite de peticiones permitidas. Intente más tarde.",
                new[] { "RATE_LIMIT_EXCEEDED" },
                context.TraceIdentifier));

            await quotaService.RegistrarUsoAsync(BuildEntry(ctx, context, ip, StatusCodes.Status429TooManyRequests, 0));
            return;
        }

        if (decision.Limit is int limit2)
        {
            context.Response.Headers["X-RateLimit-Limit"] = limit2.ToString();
            if (decision.Remaining is int rem)
                context.Response.Headers["X-RateLimit-Remaining"] = Math.Max(0, rem).ToString();
        }

        var sw = Stopwatch.StartNew();
        try
        {
            await _next(context);
        }
        finally
        {
            sw.Stop();
            await quotaService.RegistrarUsoAsync(
                BuildEntry(ctx, context, ip, context.Response.StatusCode, sw.ElapsedMilliseconds));
        }
    }

    private static ApiUsageEntry BuildEntry(QuotaContext ctx, HttpContext http, string? ip, int status, long ms) => new()
    {
        EmpresaId = ctx.EmpresaId,
        UsuarioId = ctx.UsuarioId,
        ApiKeyId = ctx.ApiKeyId,
        Metodo = http.Request.Method,
        Ruta = http.Request.Path.Value ?? "/",
        Modulo = ctx.Modulo,
        StatusCode = status,
        DuracionMs = ms,
        IpOrigen = ip,
    };

    /// <summary>Mapea el prefijo de ruta a un código de módulo lógico para cuotas por módulo.</summary>
    private static string? MapModulo(string path)
    {
        var p = path.ToLowerInvariant();
        if (p.StartsWith("/api/dte") || p.StartsWith("/api/certificacion"))
            return "NEODTE";
        if (p.StartsWith("/api/billing"))
            return "BILLING";
        if (p.StartsWith("/api/connect") || p.StartsWith("/api/v1"))
            return "NEOCONNECT";
        if (p.StartsWith("/api/")) // resto de endpoints administrativos
            return "CORE";
        return null;
    }
}
