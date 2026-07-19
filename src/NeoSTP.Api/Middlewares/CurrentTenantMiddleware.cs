using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Connect;
using NeoSTP.Application.Licenciamiento;
using NeoSTP.Shared;

namespace NeoSTP.Api.Middlewares;

/// <summary>
/// Tras la autenticación, exige que el usuario tenga EmpresaId resuelto, excepto:
/// - endpoints anónimos (login, refresh, health, openapi)
/// - SuperAdmin (puede operar sin empresa concreta, modo soporte)
/// - requests autenticados por API Key (NeoConnect) — el contexto viene en Items
/// </summary>
public class CurrentTenantMiddleware
{
    private static readonly string[] BypassPaths =
    {
        // /api/auth/* son endpoints del usuario, no del tenant
        "/api/auth/",
        "/health", "/openapi",
    };

    private readonly RequestDelegate _next;

    public CurrentTenantMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, ICurrentUser currentUser, ILicenciaGuardService licencia)
    {
        var path = context.Request.Path.Value ?? string.Empty;
        if (BypassPaths.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
        {
            await _next(context);
            return;
        }

        // Petición autenticada con API Key de NeoConnect — tenant ya resuelto,
        // pero la empresa suspendida tampoco opera vía integradores.
        if (context.Items.TryGetValue(ApiKeyAuthMiddleware.ContextItemKey, out var apiCtx)
            && apiCtx is ConnectApiKeyContext keyCtx)
        {
            if (!await licencia.EmpresaOperativaAsync(keyCtx.EmpresaId, context.RequestAborted))
            {
                await EscribirSuspendidaAsync(context);
                return;
            }
            await _next(context);
            return;
        }

        if (!currentUser.IsAuthenticated)
        {
            await _next(context);
            return;
        }

        if (currentUser.TipoUsuarioCodigo == "SUPERADMIN")
        {
            await _next(context);
            return;
        }

        if (currentUser.EmpresaId is int empresaId)
        {
            // Enforcement comercial: SUSPENDIDA/VENCIDA/INACTIVA no operan.
            if (!await licencia.EmpresaOperativaAsync(empresaId, context.RequestAborted))
            {
                await EscribirSuspendidaAsync(context);
                return;
            }
            await _next(context);
            return;
        }

        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        await context.Response.WriteAsJsonAsync(ApiResponse.Fail(
            "El usuario no tiene empresa asignada.",
            new[] { "AUTH_NO_TENANT" },
            context.TraceIdentifier));
    }

    private static async Task EscribirSuspendidaAsync(HttpContext context)
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        await context.Response.WriteAsJsonAsync(ApiResponse.Fail(
            "La empresa está suspendida o inactiva. Contacta a soporte o regulariza tu suscripción.",
            new[] { "EMPRESA_SUSPENDIDA" },
            context.TraceIdentifier));
    }
}
