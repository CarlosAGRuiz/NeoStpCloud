using Microsoft.AspNetCore.Mvc;
using NeoSTP.Api.Middlewares;
using NeoSTP.Application.Connect;
using NeoSTP.Shared;

namespace NeoSTP.Api.Controllers;

/// <summary>
/// Base para los endpoints públicos de NeoConnect (consumidos por API Key).
/// Resuelve el <see cref="ConnectApiKeyContext"/> que dejó <see cref="ApiKeyAuthMiddleware"/>
/// y centraliza el enforcement de scopes.
/// </summary>
[ApiController]
public abstract class ConnectApiControllerBase : ApiControllerBase
{
    /// <summary>Contexto de la API Key, o null si la petición no se autenticó por key.</summary>
    protected ConnectApiKeyContext? ApiKey =>
        HttpContext.Items.TryGetValue(ApiKeyAuthMiddleware.ContextItemKey, out var v)
            ? v as ConnectApiKeyContext
            : null;

    /// <summary>
    /// Verifica autenticación por API Key y que tenga el scope requerido.
    /// Devuelve la empresa resuelta o un IActionResult de error (401/403).
    /// </summary>
    protected bool TryAuthorize(string scope, out int empresaId, out IActionResult? error)
    {
        empresaId = 0;
        var ctx = ApiKey;
        if (ctx is null)
        {
            error = StatusCode(StatusCodes.Status401Unauthorized, ApiResponse.Fail(
                "Esta operación requiere autenticación por API Key (header X-Api-Key).",
                new[] { "APIKEY_REQUIRED" }, HttpContext.TraceIdentifier));
            return false;
        }

        if (!ctx.HasScope(scope))
        {
            error = StatusCode(StatusCodes.Status403Forbidden, ApiResponse.Fail(
                $"La API Key no tiene el scope requerido: {scope}.",
                new[] { "APIKEY_SCOPE_MISSING" }, HttpContext.TraceIdentifier));
            return false;
        }

        empresaId = ctx.EmpresaId;
        error = null;
        return true;
    }

    /// <summary>Actor de auditoría para acciones de API Key.</summary>
    protected string Actor => ApiKey is { ApiKeyId: var id } ? $"apikey:{id}" : "apikey";
}
