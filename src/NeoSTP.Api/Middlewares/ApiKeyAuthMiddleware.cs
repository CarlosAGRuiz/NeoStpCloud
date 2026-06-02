using NeoSTP.Application.Connect;
using NeoSTP.Shared;

namespace NeoSTP.Api.Middlewares;

/// <summary>
/// Autenticación por API Key para NeoConnect.
/// Lee el header <c>X-Api-Key</c>; si está presente valida la key contra
/// <see cref="IConnectApiKeyService"/> y almacena el contexto resuelto en
/// <c>HttpContext.Items["ConnectApiKeyCtx"]</c>.
///
/// Orden en pipeline: después de UseAuthentication, antes de CurrentTenantMiddleware.
/// Si hay JWT válido y también X-Api-Key, el JWT tiene precedencia (el middleware no actúa).
/// Si hay X-Api-Key pero es inválida → 401 inmediato.
/// </summary>
public class ApiKeyAuthMiddleware
{
    public const string ContextItemKey = "ConnectApiKeyCtx";
    private const string HeaderName = "X-Api-Key";

    private readonly RequestDelegate _next;

    public ApiKeyAuthMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context, IConnectApiKeyService apiKeyService)
    {
        // Si el usuario ya viene autenticado por JWT, no interferir.
        if (context.User.Identity?.IsAuthenticated == true)
        {
            await _next(context);
            return;
        }

        if (!context.Request.Headers.TryGetValue(HeaderName, out var headerValue)
            || string.IsNullOrWhiteSpace(headerValue))
        {
            await _next(context);
            return;
        }

        var rawKey = headerValue.ToString().Trim();
        var apiKeyCtx = await apiKeyService.ValidarAsync(rawKey, context.RequestAborted);

        if (apiKeyCtx is null)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(ApiResponse.Fail(
                "API Key inválida, revocada o expirada.",
                new[] { "APIKEY_INVALID" },
                context.TraceIdentifier));
            return;
        }

        context.Items[ContextItemKey] = apiKeyCtx;
        await _next(context);
    }
}
