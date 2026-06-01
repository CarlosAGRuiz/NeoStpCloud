using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Ops;
using NeoSTP.Shared;

namespace NeoSTP.Api.Middlewares;

/// <summary>
/// Restringe el acceso de usuarios SuperAdmin a las IP/CIDR de la lista blanca
/// (<c>Core_AdminIpAllowlist</c>). Si la lista está vacía no restringe nada.
/// Solo afecta a SuperAdmin: los usuarios de empresa no se ven impactados.
/// </summary>
public class AdminIpAllowlistMiddleware
{
    private readonly RequestDelegate _next;

    public AdminIpAllowlistMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, ICurrentUser currentUser, IAdminIpAllowlistService allowlist)
    {
        // Solo aplica a SuperAdmin autenticado.
        if (!currentUser.IsAuthenticated || currentUser.TipoUsuarioCodigo != "SUPERADMIN")
        {
            await _next(context);
            return;
        }

        var ip = context.Connection.RemoteIpAddress?.ToString();
        if (await allowlist.EstaPermitidaAsync(ip, context.RequestAborted))
        {
            await _next(context);
            return;
        }

        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        await context.Response.WriteAsJsonAsync(ApiResponse.Fail(
            "Acceso administrativo no permitido desde esta dirección IP.",
            new[] { "ADMIN_IP_NOT_ALLOWED" },
            context.TraceIdentifier));
    }
}
