using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NeoSTP.Application.Common;
using NeoSTP.Application.Connect;
using NeoSTP.Domain.Core.Connect;
using NeoSTP.Infrastructure.Persistence;

namespace NeoSTP.Infrastructure.Services;

public class ConnectApiKeyService : IConnectApiKeyService
{
    private const int RawKeyBytes = 32; // 256 bits → 43-char base64url
    private const int PrefixLength = 8;

    private readonly NeoStpDbContext _db;
    private readonly ILogger<ConnectApiKeyService> _logger;

    public ConnectApiKeyService(NeoStpDbContext db, ILogger<ConnectApiKeyService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<Result<CrearApiKeyResult>> CrearAsync(CrearApiKeyRequest request, string? actor, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Nombre))
            return Result<CrearApiKeyResult>.Fail("El nombre es obligatorio.", "VALIDATION");

        if (request.Scopes.Length == 0)
            return Result<CrearApiKeyResult>.Fail("Debe indicar al menos un scope.", "VALIDATION");

        var rawKey = GenerateRawKey();
        var prefix = rawKey[..PrefixLength];
        var hash = HashKey(rawKey);

        var entity = new ConnectApiKey
        {
            EmpresaId = request.EmpresaId,
            Nombre = request.Nombre.Trim(),
            Prefix = prefix,
            KeyHash = hash,
            Scopes = string.Join(",", request.Scopes),
            Activo = true,
            ExpiresAt = request.ExpiresAt,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = actor,
        };

        _db.ConnectApiKeys.Add(entity);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("ConnectApiKey creada: id={Id} empresa={EmpresaId} nombre={Nombre}",
            entity.Id, entity.EmpresaId, entity.Nombre);

        var dto = ToDto(entity);
        return Result<CrearApiKeyResult>.Ok(new CrearApiKeyResult { Key = dto, RawKey = rawKey });
    }

    public async Task<IReadOnlyList<ConnectApiKeyDto>> ListarAsync(int empresaId, CancellationToken ct = default)
    {
        var keys = await _db.ConnectApiKeys.AsNoTracking()
            .Where(k => k.EmpresaId == empresaId)
            .OrderByDescending(k => k.CreatedAt)
            .ToListAsync(ct);

        return keys.Select(ToDto).ToList();
    }

    public async Task<Result<ConnectApiKeyDto>> ObtenerAsync(int id, int empresaId, CancellationToken ct = default)
    {
        var key = await _db.ConnectApiKeys.AsNoTracking()
            .FirstOrDefaultAsync(k => k.Id == id && k.EmpresaId == empresaId, ct);

        if (key is null)
            return Result<ConnectApiKeyDto>.Fail("API Key no encontrada.", "NOT_FOUND");

        return Result<ConnectApiKeyDto>.Ok(ToDto(key));
    }

    public async Task<Result> RevocarAsync(int id, int empresaId, string? actor, CancellationToken ct = default)
    {
        var key = await _db.ConnectApiKeys
            .FirstOrDefaultAsync(k => k.Id == id && k.EmpresaId == empresaId, ct);

        if (key is null)
            return Result.Fail("API Key no encontrada.", "NOT_FOUND");

        if (!key.Activo)
            return Result.Fail("La API Key ya está revocada.", "ALREADY_REVOKED");

        key.Activo = false;
        key.RevokedAt = DateTime.UtcNow;
        key.RevokedBy = actor;
        key.UpdatedAt = DateTime.UtcNow;
        key.UpdatedBy = actor;

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("ConnectApiKey revocada: id={Id} empresa={EmpresaId} por={Actor}",
            id, empresaId, actor);

        return Result.Ok();
    }

    public async Task<ConnectApiKeyContext?> ValidarAsync(string rawKey, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(rawKey))
            return null;

        var hash = HashKey(rawKey);

        var key = await _db.ConnectApiKeys.AsNoTracking()
            .FirstOrDefaultAsync(k => k.KeyHash == hash && k.Activo, ct);

        if (key is null)
            return null;

        if (key.ExpiresAt.HasValue && key.ExpiresAt.Value < DateTime.UtcNow)
            return null;

        // Actualizar UltimoUsoAt (best-effort, no rompe si falla)
        try
        {
            await _db.ConnectApiKeys
                .Where(k => k.Id == key.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(k => k.UltimoUsoAt, DateTime.UtcNow), ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo actualizar UltimoUsoAt para ApiKey id={Id}", key.Id);
        }

        return new ConnectApiKeyContext
        {
            ApiKeyId = key.Id,
            EmpresaId = key.EmpresaId,
            Scopes = key.Scopes.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
        };
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private static string GenerateRawKey()
    {
        var bytes = RandomNumberGenerator.GetBytes(RawKeyBytes);
        // Base64Url sin padding: caracteres URL-safe, sin =
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    internal static string HashKey(string rawKey)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(rawKey);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static ConnectApiKeyDto ToDto(ConnectApiKey k) => new()
    {
        Id = k.Id,
        EmpresaId = k.EmpresaId,
        Nombre = k.Nombre,
        Prefix = k.Prefix,
        Scopes = k.Scopes.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
        Activo = k.Activo,
        ExpiresAt = k.ExpiresAt,
        UltimoUsoAt = k.UltimoUsoAt,
        RevokedAt = k.RevokedAt,
        RevokedBy = k.RevokedBy,
        CreatedAt = k.CreatedAt,
    };
}
