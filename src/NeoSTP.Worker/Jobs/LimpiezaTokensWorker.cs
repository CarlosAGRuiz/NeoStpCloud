using Microsoft.Extensions.Options;
using NeoSTP.Application.Workers;

namespace NeoSTP.Worker.Jobs;

/// <summary>
/// Job periódico que elimina refresh tokens vencidos o revocados
/// que superan el período de retención configurado.
/// Se ejecuta cada <see cref="LimpiezaTokensOptions.IntervaloHoras"/> horas.
/// </summary>
public class LimpiezaTokensWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<LimpiezaTokensWorker> _logger;
    private readonly TimeSpan _intervalo;

    public LimpiezaTokensWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<WorkerOptions> options,
        ILogger<LimpiezaTokensWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _intervalo = TimeSpan.FromHours(options.Value.LimpiezaTokens.IntervaloHoras);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "LimpiezaTokensWorker iniciado. Intervalo: {Intervalo}",
            _intervalo);

        // Primera ejecución a los 30 segundos del arranque
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
                _logger.LogError(ex, "LimpiezaTokensWorker: error inesperado");
            }

            await Task.Delay(_intervalo, stoppingToken);
        }

        _logger.LogInformation("LimpiezaTokensWorker detenido");
    }

    private async Task EjecutarAsync(CancellationToken ct)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<ILimpiezaTokensService>();

        var eliminados = await service.LimpiarTokensVencidosAsync(ct);
        if (eliminados > 0)
            _logger.LogInformation("LimpiezaTokensWorker: {Count} tokens eliminados", eliminados);
        else
            _logger.LogDebug("LimpiezaTokensWorker: sin tokens para limpiar");
    }
}
