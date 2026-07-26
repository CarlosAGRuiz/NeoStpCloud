using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NeoSTP.Application.Notificaciones;
using NeoSTP.Application.Workers;
using NeoSTP.Infrastructure.Persistence;

namespace NeoSTP.Worker.Jobs;

/// <summary>
/// Job periódico que genera alertas (DTE rechazado, certificado por vencer, facturas vencidas)
/// para cada empresa activa, usando <see cref="IAlertaGeneracionService"/> (idempotente, dedupe por clave).
/// </summary>
public class AlertaGeneracionWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AlertaGeneracionWorker> _logger;
    private readonly TimeSpan _intervalo;

    public AlertaGeneracionWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<WorkerOptions> options,
        ILogger<AlertaGeneracionWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _intervalo = TimeSpan.FromMinutes(Math.Max(1, options.Value.GeneracionAlertas.IntervaloMinutos));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("AlertaGeneracionWorker iniciado. Intervalo: {Intervalo}min", _intervalo.TotalMinutes);
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await EjecutarAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "AlertaGeneracionWorker: error inesperado");
            }

            await Task.Delay(_intervalo, stoppingToken);
        }

        _logger.LogInformation("AlertaGeneracionWorker detenido");
    }

    private async Task EjecutarAsync(CancellationToken ct)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<NeoStpDbContext>();
        var generacion = scope.ServiceProvider.GetRequiredService<IAlertaGeneracionService>();

        var empresas = await db.Empresas.AsNoTracking()
            .Where(e => e.EstadoCodigo == NeoSTP.Domain.Common.EmpresaEstados.Activa)
            .Select(e => e.Id)
            .ToListAsync(ct);

        var totalCreadas = 0;
        foreach (var empresaId in empresas)
        {
            try
            {
                totalCreadas += await generacion.GenerarAsync(empresaId, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "AlertaGeneracionWorker: error generando alertas para empresa {EmpresaId}", empresaId);
            }
        }

        if (totalCreadas > 0)
            _logger.LogInformation("AlertaGeneracionWorker: {N} alertas nuevas en {E} empresas", totalCreadas, empresas.Count);
    }
}
