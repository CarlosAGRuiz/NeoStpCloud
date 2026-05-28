using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NeoSTP.Application.Dte;
using NeoSTP.Application.Workers;
using NeoSTP.Domain.Core.Dte;
using NeoSTP.Infrastructure.Persistence;

namespace NeoSTP.Infrastructure.Services;

/// <summary>
/// Retransmite automáticamente los DTE en estado CONTINGENCIA que no han superado
/// el número máximo de intentos y han esperado el tiempo de cooldown configurado.
/// </summary>
public class DteRetransmisionService : IDteRetransmisionService
{
    private readonly NeoStpDbContext _db;
    private readonly IDteDocumentosService _dteService;
    private readonly WorkerOptions _options;
    private readonly ILogger<DteRetransmisionService> _logger;

    public DteRetransmisionService(
        NeoStpDbContext db,
        IDteDocumentosService dteService,
        IOptions<WorkerOptions> options,
        ILogger<DteRetransmisionService> logger)
    {
        _db = db;
        _dteService = dteService;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<RetransmisionResultado> RetransmitirContingenciasAsync(CancellationToken ct = default)
    {
        var opts = _options.RetransmisionContingencia;
        var ahora = DateTime.UtcNow;
        var cooldownLimite = ahora.AddMinutes(-opts.CooldownMinutos);

        // Seleccionar candidatos: CONTINGENCIA, sin superar máx intentos, cooldown cumplido
        var candidatos = await _db.DteDocumentos
            .AsNoTracking()
            .Where(d => d.EstadoCodigo == DteEstadoCodigos.Contingencia
                     && d.IntentoRetransmision < opts.MaxIntentos
                     && (d.UltimoIntentoRetransmisionAt == null
                         || d.UltimoIntentoRetransmisionAt < cooldownLimite))
            .OrderBy(d => d.UltimoIntentoRetransmisionAt ?? d.CreatedAt)
            .Take(opts.LoteMaximo)
            .Select(d => new { d.Id, d.EmpresaId, d.NumeroControl, d.IntentoRetransmision })
            .ToListAsync(ct);

        var resultado = new RetransmisionResultado { Procesados = candidatos.Count };

        if (candidatos.Count == 0)
        {
            _logger.LogDebug("RetransmisionContingencia: sin documentos elegibles");
            return resultado;
        }

        _logger.LogInformation("RetransmisionContingencia: procesando {Count} documentos CONTINGENCIA", candidatos.Count);

        foreach (var c in candidatos)
        {
            if (ct.IsCancellationRequested) break;

            // Marcar intento ANTES del envío para evitar reintento paralelo
            var entity = await _db.DteDocumentos.FindAsync([c.Id], ct);
            if (entity is null)
            {
                resultado.Omitidos++;
                continue;
            }

            entity.IntentoRetransmision++;
            entity.UltimoIntentoRetransmisionAt = ahora;
            await _db.SaveChangesAsync(ct);

            try
            {
                var r = await _dteService.EnviarAsync(c.EmpresaId, c.Id, "WORKER", ct);

                if (r.IsSuccess)
                {
                    var estadoFinal = r.Value?.EstadoCodigo ?? "?";
                    var msg = $"{c.NumeroControl} → {estadoFinal} (intento #{entity.IntentoRetransmision})";
                    resultado.Detalles.Add(msg);

                    if (estadoFinal == DteEstadoCodigos.Procesado)
                    {
                        resultado.Exitosos++;
                        _logger.LogInformation("Retransmisión EXITOSA: {Msg}", msg);
                    }
                    else
                    {
                        // Todavía CONTINGENCIA u otro estado: cuenta como fallida
                        resultado.Fallidos++;
                        _logger.LogWarning("Retransmisión sin procesar: {Msg}", msg);
                    }
                }
                else
                {
                    resultado.Fallidos++;
                    var msg = $"{c.NumeroControl} → ERROR: {r.Error}";
                    resultado.Detalles.Add(msg);
                    _logger.LogWarning("Retransmisión fallida: {Msg}", msg);
                }
            }
            catch (Exception ex)
            {
                resultado.Fallidos++;
                var msg = $"{c.NumeroControl} → EXCEPCION: {ex.Message}";
                resultado.Detalles.Add(msg);
                _logger.LogError(ex, "Retransmisión excepción: {NumeroControl}", c.NumeroControl);
            }
        }

        _logger.LogInformation(
            "RetransmisionContingencia completada: {Exitosos} exitosos, {Fallidos} fallidos, {Omitidos} omitidos",
            resultado.Exitosos, resultado.Fallidos, resultado.Omitidos);

        return resultado;
    }
}
