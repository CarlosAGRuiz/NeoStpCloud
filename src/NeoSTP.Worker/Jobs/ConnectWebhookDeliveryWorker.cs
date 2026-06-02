using Microsoft.Extensions.Options;
using NeoSTP.Application.Connect;
using NeoSTP.Application.Workers;

namespace NeoSTP.Worker.Jobs;

/// <summary>
/// Job periódico que entrega las notificaciones de webhook pendientes de NeoConnect.
/// Usa <see cref="IConnectWebhookDispatcher.ProcesarPendientesAsync"/> para enviar los
/// payloads firmados HMAC-SHA256 con reintentos y backoff exponencial.
/// </summary>
public class ConnectWebhookDeliveryWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ConnectWebhookDeliveryWorker> _logger;
    private readonly TimeSpan _intervalo;

    public ConnectWebhookDeliveryWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<WorkerOptions> options,
        ILogger<ConnectWebhookDeliveryWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _intervalo = TimeSpan.FromSeconds(options.Value.WebhookDelivery.IntervaloSegundos);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "ConnectWebhookDeliveryWorker iniciado. Intervalo: {Intervalo}s", _intervalo.TotalSeconds);

        await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);

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
                _logger.LogError(ex, "ConnectWebhookDeliveryWorker: error inesperado");
            }

            await Task.Delay(_intervalo, stoppingToken);
        }

        _logger.LogInformation("ConnectWebhookDeliveryWorker detenido");
    }

    private async Task EjecutarAsync(CancellationToken ct)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IConnectWebhookDispatcher>();

        var procesados = await dispatcher.ProcesarPendientesAsync(ct);
        if (procesados > 0)
            _logger.LogInformation("ConnectWebhookDeliveryWorker: {N} entregas procesadas", procesados);
    }
}
