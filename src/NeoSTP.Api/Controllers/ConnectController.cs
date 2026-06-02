using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NeoSTP.Api.Authorization;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Connect;
using NeoSTP.Shared;

namespace NeoSTP.Api.Controllers;

/// <summary>
/// NeoConnect API — gestión de API Keys, webhooks, logs y consumo.
/// Todos los endpoints requieren autenticación JWT (la empresa gestiona sus propias integraciones).
/// </summary>
[Authorize]
[Route("api/connect")]
public class ConnectController : ApiControllerBase
{
    private readonly IConnectApiKeyService _apiKeys;
    private readonly IConnectWebhookService _webhooks;
    private readonly ICurrentUser _currentUser;

    public ConnectController(
        IConnectApiKeyService apiKeys,
        IConnectWebhookService webhooks,
        ICurrentUser currentUser)
    {
        _apiKeys = apiKeys;
        _webhooks = webhooks;
        _currentUser = currentUser;
    }

    // ─── API Keys ─────────────────────────────────────────────────────────────

    /// <summary>Lista las API Keys de la empresa.</summary>
    [HttpGet("api-keys")]
    [RequirePermiso("Connect.ApiKeys.Ver")]
    public async Task<IActionResult> ListarApiKeys(CancellationToken ct)
    {
        var keys = await _apiKeys.ListarAsync(EmpresaId, ct);
        return Ok(ApiResponse<IReadOnlyList<ConnectApiKeyDto>>.Ok(keys, traceId: HttpContext.TraceIdentifier));
    }

    /// <summary>
    /// Crea una nueva API Key.
    /// La raw key se devuelve UNA SOLA VEZ en esta respuesta — no se vuelve a mostrar.
    /// </summary>
    [HttpPost("api-keys")]
    [RequirePermiso("Connect.ApiKeys.Administrar")]
    public async Task<IActionResult> CrearApiKey([FromBody] CrearApiKeyRequest request, CancellationToken ct)
    {
        var req = request with { EmpresaId = EmpresaId };
        return Respond(await _apiKeys.CrearAsync(req, _currentUser.Username, ct));
    }

    /// <summary>Obtiene el detalle de una API Key.</summary>
    [HttpGet("api-keys/{id:int}")]
    [RequirePermiso("Connect.ApiKeys.Ver")]
    public async Task<IActionResult> ObtenerApiKey(int id, CancellationToken ct)
        => Respond(await _apiKeys.ObtenerAsync(id, EmpresaId, ct));

    /// <summary>Revoca una API Key. La acción es irreversible.</summary>
    [HttpPatch("api-keys/{id:int}/revoke")]
    [RequirePermiso("Connect.ApiKeys.Administrar")]
    public async Task<IActionResult> RevocarApiKey(int id, CancellationToken ct)
        => Respond(await _apiKeys.RevocarAsync(id, EmpresaId, _currentUser.Username, ct), "API Key revocada.");

    // ─── Webhooks ─────────────────────────────────────────────────────────────

    /// <summary>Lista los webhooks configurados por la empresa.</summary>
    [HttpGet("webhooks")]
    [RequirePermiso("Connect.Webhooks.Ver")]
    public async Task<IActionResult> ListarWebhooks(CancellationToken ct)
    {
        var list = await _webhooks.ListarAsync(EmpresaId, ct);
        return Ok(ApiResponse<IReadOnlyList<ConnectWebhookDto>>.Ok(list, traceId: HttpContext.TraceIdentifier));
    }

    /// <summary>Crea un nuevo webhook. El secreto HMAC se genera automáticamente.</summary>
    [HttpPost("webhooks")]
    [RequirePermiso("Connect.Webhooks.Administrar")]
    public async Task<IActionResult> CrearWebhook([FromBody] CrearWebhookRequest request, CancellationToken ct)
    {
        var req = request with { EmpresaId = EmpresaId };
        return Respond(await _webhooks.CrearAsync(req, _currentUser.Username, ct));
    }

    /// <summary>Obtiene el detalle de un webhook.</summary>
    [HttpGet("webhooks/{id:int}")]
    [RequirePermiso("Connect.Webhooks.Ver")]
    public async Task<IActionResult> ObtenerWebhook(int id, CancellationToken ct)
        => Respond(await _webhooks.ObtenerAsync(id, EmpresaId, ct));

    /// <summary>Elimina un webhook y sus entregas.</summary>
    [HttpDelete("webhooks/{id:int}")]
    [RequirePermiso("Connect.Webhooks.Administrar")]
    public async Task<IActionResult> EliminarWebhook(int id, CancellationToken ct)
        => Respond(await _webhooks.EliminarAsync(id, EmpresaId, _currentUser.Username, ct), "Webhook eliminado.");

    /// <summary>Envía un payload de prueba al webhook para verificar la conectividad.</summary>
    [HttpPost("webhooks/{id:int}/test")]
    [RequirePermiso("Connect.Webhooks.Administrar")]
    public async Task<IActionResult> TestWebhook(int id, CancellationToken ct)
        => Respond(await _webhooks.TestAsync(id, EmpresaId, _currentUser.Username, ct));

    // ─── Logs ─────────────────────────────────────────────────────────────────

    /// <summary>Lista las entregas de webhook más recientes de la empresa (máx. 100).</summary>
    [HttpGet("logs")]
    [RequirePermiso("Connect.Logs.Ver")]
    public async Task<IActionResult> Logs([FromQuery] int limit = 50, CancellationToken ct = default)
    {
        limit = Math.Clamp(limit, 1, 100);
        var list = await _webhooks.ListarDeliveriesAsync(EmpresaId, limit, ct);
        return Ok(ApiResponse<IReadOnlyList<ConnectWebhookDeliveryDto>>.Ok(list, traceId: HttpContext.TraceIdentifier));
    }

    // ─── Usage ────────────────────────────────────────────────────────────────

    /// <summary>Resumen de consumo (peticiones + webhooks) agrupado por API Key.</summary>
    [HttpGet("usage")]
    [RequirePermiso("Connect.Logs.Ver")]
    public async Task<IActionResult> Usage(CancellationToken ct)
    {
        var data = await _webhooks.ObtenerUsageAsync(EmpresaId, ct);
        return Ok(ApiResponse<IReadOnlyList<ConnectUsageDto>>.Ok(data, traceId: HttpContext.TraceIdentifier));
    }

    // ─── Helper ───────────────────────────────────────────────────────────────

    private int EmpresaId => _currentUser.EmpresaId
        ?? throw new InvalidOperationException("EmpresaId no resuelto.");
}
