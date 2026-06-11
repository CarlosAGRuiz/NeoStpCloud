using Microsoft.Extensions.Options;
using NeoSTP.Application.Ops;
using NeoSTP.Application.Workers;

namespace NeoSTP.Worker.Jobs;

/// <summary>
/// V2.5-S5 — purga programada de auditoría por retención. Deshabilitado por defecto:
/// se activa con Worker:LimpiezaAuditoria:Enabled=true (retención mínima 30 días).
/// </summary>
public class LimpiezaAuditoriaWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<LimpiezaAuditoriaWorker> _logger;
    private readonly LimpiezaAuditoriaOptions _options;

    public LimpiezaAuditoriaWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<WorkerOptions> options,
        ILogger<LimpiezaAuditoriaWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _options = options.Value.LimpiezaAuditoria;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("LimpiezaAuditoriaWorker deshabilitado (Worker:LimpiezaAuditoria:Enabled=false).");
            return;
        }

        var intervalo = TimeSpan.FromHours(Math.Max(1, _options.IntervaloHoras));
        _logger.LogInformation("LimpiezaAuditoriaWorker iniciado. Retención: {Dias}d, intervalo: {Horas}h",
            _options.RetencionDias, intervalo.TotalHours);
        await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var limpieza = scope.ServiceProvider.GetRequiredService<ILimpiezaAuditoriaService>();
                var purgados = await limpieza.PurgarAsync(_options.RetencionDias, _options.BatchSize, stoppingToken);
                if (purgados > 0)
                    _logger.LogInformation("LimpiezaAuditoriaWorker: {Purgados} eventos purgados.", purgados);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "LimpiezaAuditoriaWorker: error inesperado");
            }

            await Task.Delay(intervalo, stoppingToken);
        }

        _logger.LogInformation("LimpiezaAuditoriaWorker detenido");
    }
}
