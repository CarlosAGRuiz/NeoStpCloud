using Microsoft.Extensions.Options;
using NeoSTP.Application.Workers;

namespace NeoSTP.Worker.Jobs;

/// <summary>
/// Job periódico que retransmite a Hacienda los DTE en estado CONTINGENCIA.
/// Se ejecuta cada <see cref="RetransmisionContingenciaOptions.IntervaloMinutos"/> minutos.
///
/// Usa <see cref="IServiceScopeFactory"/> para crear un scope DI por ejecución
/// (los servicios de aplicación son scoped; los BackgroundService son singleton).
/// </summary>
public class RetransmisionContingenciaWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<RetransmisionContingenciaWorker> _logger;
    private TimeSpan _intervalo;

    public RetransmisionContingenciaWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<WorkerOptions> options,
        ILogger<RetransmisionContingenciaWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _intervalo = TimeSpan.FromMinutes(options.Value.RetransmisionContingencia.IntervaloMinutos);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "RetransmisionContingenciaWorker iniciado. Intervalo: {Intervalo}",
            _intervalo);

        // Espera breve al arranque para que la BD esté lista
        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);

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
                _logger.LogError(ex, "RetransmisionContingenciaWorker: error inesperado");
            }

            await Task.Delay(_intervalo, stoppingToken);
        }

        _logger.LogInformation("RetransmisionContingenciaWorker detenido");
    }

    private async Task EjecutarAsync(CancellationToken ct)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IDteRetransmisionService>();

        _logger.LogDebug("RetransmisionContingenciaWorker: iniciando ciclo");
        var resultado = await service.RetransmitirContingenciasAsync(ct);

        if (resultado.Procesados > 0)
        {
            _logger.LogInformation(
                "RetransmisionContingenciaWorker: {Proc} procesados — {Ok} exitosos, {Fail} fallidos, {Skip} omitidos",
                resultado.Procesados, resultado.Exitosos, resultado.Fallidos, resultado.Omitidos);
        }
    }
}
