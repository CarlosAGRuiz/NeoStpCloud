using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Connect;
using NeoSTP.Application.Empresas;
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

    public async Task InvokeAsync(
        HttpContext context,
        ICurrentUser currentUser,
        ILicenciaGuardService licencia,
        ILicenciaResolver licencias)
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

            var moduloRequerido = ModuloApiKey(path);
            if (moduloRequerido is not null)
            {
                var licenciaActual = await licencias.ResolveAsync(keyCtx.EmpresaId, context.RequestAborted);
                if (licenciaActual is null || !licenciaActual.Vigente)
                {
                    await EscribirLicenciaInvalidaAsync(context);
                    return;
                }

                var moduloActivo = licenciaActual.Modulos.Any(m =>
                    m.Activo && string.Equals(m.Codigo, moduloRequerido, StringComparison.OrdinalIgnoreCase));
                if (!moduloActivo)
                {
                    await EscribirModuloNoLicenciadoAsync(context, moduloRequerido);
                    return;
                }
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

    private static string? ModuloApiKey(string path)
    {
        if (path.StartsWith("/api/v1/dte", StringComparison.OrdinalIgnoreCase)) return "NEODTE";
        if (path.StartsWith("/api/v1/clientes", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/api/v1/productos", StringComparison.OrdinalIgnoreCase)) return "CORE";
        return null;
    }

    private static async Task EscribirLicenciaInvalidaAsync(HttpContext context)
    {
        context.Response.StatusCode = StatusCodes.Status402PaymentRequired;
        await context.Response.WriteAsJsonAsync(ApiResponse.Fail(
            "La empresa no tiene un plan vigente para usar esta API.",
            new[] { "LICENSE_INVALID" },
            context.TraceIdentifier));
    }

    private static async Task EscribirModuloNoLicenciadoAsync(HttpContext context, string modulo)
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        await context.Response.WriteAsJsonAsync(ApiResponse.Fail(
            $"El módulo {modulo} no está habilitado para la empresa.",
            new[] { "MODULE_NOT_LICENSED" },
            context.TraceIdentifier));
    }
}
