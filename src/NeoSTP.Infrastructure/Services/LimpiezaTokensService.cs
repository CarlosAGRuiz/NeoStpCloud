using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NeoSTP.Application.Workers;
using NeoSTP.Infrastructure.Persistence;

namespace NeoSTP.Infrastructure.Services;

/// <summary>
/// Elimina refresh tokens vencidos o revocados que superan el período de retención.
/// Reduce el crecimiento indefinido de la tabla <c>Core_RefreshTokens</c>.
/// </summary>
public class LimpiezaTokensService : ILimpiezaTokensService
{
    private readonly NeoStpDbContext _db;
    private readonly WorkerOptions _options;
    private readonly ILogger<LimpiezaTokensService> _logger;

    public LimpiezaTokensService(
        NeoStpDbContext db,
        IOptions<WorkerOptions> options,
        ILogger<LimpiezaTokensService> logger)
    {
        _db = db;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<int> LimpiarTokensVencidosAsync(CancellationToken ct = default)
    {
        var retentionDias = _options.LimpiezaTokens.RetentionDias;
        var limite = DateTime.UtcNow.AddDays(-retentionDias);

        // Tokens expirados hace más de retentionDias
        // O revocados hace más de retentionDias
        var tokens = await _db.RefreshTokens
            .Where(t => (t.ExpiresAt < limite)
                     || (t.RevokedAt.HasValue && t.RevokedAt.Value < limite))
            .ToListAsync(ct);

        if (tokens.Count == 0)
        {
            _logger.LogDebug("LimpiezaTokens: sin tokens elegibles para limpieza");
            return 0;
        }

        _db.RefreshTokens.RemoveRange(tokens);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "LimpiezaTokens: eliminados {Count} refresh tokens con retención > {Dias} días",
            tokens.Count, retentionDias);

        return tokens.Count;
    }
}
