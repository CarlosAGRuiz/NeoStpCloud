using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NeoSTP.Application.Ops;
using NeoSTP.Infrastructure.Persistence;

namespace NeoSTP.Infrastructure.Services;

/// <summary>
/// V2.5-S5 — purga por lotes de Auditoria (Core_Auditoria). Borra con RemoveRange en
/// tandas acotadas para no mantener bloqueos largos ni inflar el log de transacciones;
/// compatible con SQL Server e InMemory (tests).
/// </summary>
public class LimpiezaAuditoriaService : ILimpiezaAuditoriaService
{
    private readonly NeoStpDbContext _db;
    private readonly ILogger<LimpiezaAuditoriaService> _logger;

    public LimpiezaAuditoriaService(NeoStpDbContext db, ILogger<LimpiezaAuditoriaService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<int> PurgarAsync(int retencionDias, int batchSize = 5000, CancellationToken ct = default)
    {
        retencionDias = Math.Max(30, retencionDias); // red de seguridad: nunca menos de 30 días
        batchSize = Math.Clamp(batchSize, 100, 50_000);
        var corte = DateTime.UtcNow.AddDays(-retencionDias);

        var total = 0;
        while (!ct.IsCancellationRequested)
        {
            var lote = await _db.Auditoria
                .Where(a => a.CreatedAt < corte)
                .OrderBy(a => a.Id)
                .Take(batchSize)
                .ToListAsync(ct);
            if (lote.Count == 0) break;

            _db.Auditoria.RemoveRange(lote);
            await _db.SaveChangesAsync(ct);
            total += lote.Count;
        }

        if (total > 0)
            _logger.LogInformation("Auditoría purgada: {Total} eventos anteriores a {Corte:yyyy-MM-dd}.", total, corte);
        return total;
    }
}
