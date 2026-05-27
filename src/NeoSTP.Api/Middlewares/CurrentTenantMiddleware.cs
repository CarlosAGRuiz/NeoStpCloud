using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Shared;

namespace NeoSTP.Api.Middlewares;

/// <summary>
/// Tras la autenticación, exige que el usuario tenga EmpresaId resuelto, excepto:
/// - endpoints anónimos (login, refresh, health, openapi)
/// - SuperAdmin (puede operar sin empresa concreta, modo soporte)
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

    public async Task InvokeAsync(HttpContext context, ICurrentUser currentUser)
    {
        var path = context.Request.Path.Value ?? string.Empty;
        if (BypassPaths.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
        {
            await _next(context);
            return;
        }

        if (!currentUser.IsAuthenticated)
        {
            await _next(context);
            return;
        }

        if (currentUser.TipoUsuarioCodigo == "SUPERADMIN" || currentUser.EmpresaId is not null)
        {
            await _next(context);
            return;
        }

        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        await context.Response.WriteAsJsonAsync(ApiResponse.Fail(
            "El usuario no tiene empresa asignada.",
            new[] { "AUTH_NO_TENANT" },
            context.TraceIdentifier));
    }
}
