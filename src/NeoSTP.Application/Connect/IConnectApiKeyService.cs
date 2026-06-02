using NeoSTP.Application.Common;

namespace NeoSTP.Application.Connect;

// ─── DTOs ────────────────────────────────────────────────────────────────────

public sealed record ConnectApiKeyDto
{
    public int Id { get; init; }
    public int EmpresaId { get; init; }
    public string Nombre { get; init; } = null!;
    public string Prefix { get; init; } = null!;
    public string[] Scopes { get; init; } = [];
    public bool Activo { get; init; }
    public DateTime? ExpiresAt { get; init; }
    public DateTime? UltimoUsoAt { get; init; }
    public DateTime? RevokedAt { get; init; }
    public string? RevokedBy { get; init; }
    public DateTime CreatedAt { get; init; }
}

public sealed record CrearApiKeyRequest
{
    public int EmpresaId { get; init; }
    public string Nombre { get; init; } = null!;
    public string[] Scopes { get; init; } = [];
    public DateTime? ExpiresAt { get; init; }
}

/// <summary>
/// Resultado de crear una API Key. Incluye <see cref="RawKey"/> solo en esta respuesta;
/// nunca se almacena ni se vuelve a devolver.
/// </summary>
public sealed record CrearApiKeyResult
{
    public ConnectApiKeyDto Key { get; init; } = null!;
    /// <summary>Key en texto plano. Mostrar al usuario una sola vez y descartarla.</summary>
    public string RawKey { get; init; } = null!;
}

// ─── Contexto resuelto por el middleware ─────────────────────────────────────

/// <summary>
/// Contexto de autenticación por API Key. Se almacena en
/// <c>HttpContext.Items["ConnectApiKeyCtx"]</c> cuando la key es válida.
/// </summary>
public sealed record ConnectApiKeyContext
{
    public int ApiKeyId { get; init; }
    public int EmpresaId { get; init; }
    public string[] Scopes { get; init; } = [];

    public bool HasScope(string scope) =>
        Scopes.Contains(scope, StringComparer.OrdinalIgnoreCase);
}

// ─── Interfaz ────────────────────────────────────────────────────────────────

/// <summary>
/// Gestión de API Keys de NeoConnect: creación (con hash SHA-256), listado, revocación
/// y validación en el middleware de autenticación.
/// </summary>
public interface IConnectApiKeyService
{
    Task<Result<CrearApiKeyResult>> CrearAsync(CrearApiKeyRequest request, string? actor, CancellationToken ct = default);
    Task<IReadOnlyList<ConnectApiKeyDto>> ListarAsync(int empresaId, CancellationToken ct = default);
    Task<Result<ConnectApiKeyDto>> ObtenerAsync(int id, int empresaId, CancellationToken ct = default);
    Task<Result> RevocarAsync(int id, int empresaId, string? actor, CancellationToken ct = default);

    /// <summary>
    /// Valida una raw API Key recibida en el header <c>X-Api-Key</c>.
    /// Actualiza <c>UltimoUsoAt</c> si es válida.
    /// Devuelve <c>null</c> si la key no existe, está revocada o expiró.
    /// </summary>
    Task<ConnectApiKeyContext?> ValidarAsync(string rawKey, CancellationToken ct = default);
}
